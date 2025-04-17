using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonalResearchAssistantV5.Data;
using PersonalResearchAssistantV5.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace PersonalResearchAssistantV5.Controllers
{
    [Authorize]
    public class PdfController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;

        public PdfController(ApplicationDbContext context, IWebHostEnvironment env, UserManager<ApplicationUser> userManager, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost]
        public async Task<IActionResult> UploadPdf(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid PDF file.";
                return RedirectToAction("UserHome", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Ensure "uploads" directory exists
            string uploadsFolder = System.IO.Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            string filePath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var newPdf = new UserPdf
            {
                UserId = user.Id,
                FileName = file.FileName,
                FilePath = "uploads/" + uniqueFileName
            };

            _context.UserPdfs.Add(newPdf);
            await _context.SaveChangesAsync();

            TempData["Success"] = "PDF uploaded successfully.";
            return RedirectToAction("UserHome", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> DeletePdf(int id)
        {
            var pdf = await _context.UserPdfs.FindAsync(id);
            if (pdf == null)
            {
                return NotFound();
            }

            var filePath = System.IO.Path.Combine(_env.WebRootPath, pdf.FilePath);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            _context.UserPdfs.Remove(pdf);
            await _context.SaveChangesAsync();

            return RedirectToAction("UserHome", "Home");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminDeletePdf(int pdfId)
        {
            var pdf = await _context.UserPdfs.FindAsync(pdfId);
            if (pdf == null)
            {
                return NotFound();
            }

            var filePath = System.IO.Path.Combine(_env.WebRootPath, pdf.FilePath);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            _context.UserPdfs.Remove(pdf);
            await _context.SaveChangesAsync();

            return RedirectToAction("AdminHome", "Home");
        }

        public async Task<IActionResult> ExtractText(int id)
        {
            var pdf = await _context.UserPdfs.FindAsync(id);
            if (pdf == null)
            {
                return NotFound();
            }

            string fullPath = System.IO.Path.Combine(_env.WebRootPath, pdf.FilePath);
            Console.WriteLine($"[DEBUG] Extracting text from: {fullPath}");

            if (!System.IO.File.Exists(fullPath))
            {
                Console.WriteLine($"[ERROR] File not found at: {fullPath}");
                return NotFound($"File not found at: {fullPath}");
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var (extractedText, title, author, publicationDate) = ExtractTextFromPdf(fullPath);
            var analysisResult = await ProcessWithOpenAI(extractedText);

            ExtractTextViewModel model = new ExtractTextViewModel
            {
                FileName = pdf.FileName,
                ExtractedText = extractedText,
                Title = title,
                Author = author,
                PublicationDate = publicationDate,
                KeywordFrequency = analysisResult.Keywords ?? new Dictionary<string, int>()
            };

            return View("ExtractedText", model);
        }

        private (string extractedText, string title, string author, string publicationDate) ExtractTextFromPdf(string filePath)
        {
            try
            {
                using var reader = new PdfReader(filePath);
                StringBuilder text = new StringBuilder();

                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    text.Append(PdfTextExtractor.GetTextFromPage(reader, i));
                }

                var metadata = reader.Info;
                string title = metadata.ContainsKey("Title") ? metadata["Title"] : "Unknown Title";
                string author = metadata.ContainsKey("Author") ? metadata["Author"] : "Unknown Author";
                string publicationDate = metadata.ContainsKey("CreationDate") ? metadata["CreationDate"] : "Unknown Date";

                return (text.ToString(), title, author, publicationDate);
            }
            catch (Exception ex)
            {
                return ($"Error extracting text: {ex.Message}", "Unknown Title", "Unknown Author", "Unknown Date");
            }
        }

        private async Task<OpenAIResponse> ProcessWithOpenAI(string text)
        {
            string apiKey = "";
            string apiUrl = "https://api.openai.com/v1/chat/completions";

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                model = "gpt-4",
                messages = new[]
                {
                    new { role = "system", content = "Analyze the text for title, author, and key topics." },
                    new { role = "user", content = text }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(apiUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            return ParseOpenAIResponse(responseString);
        }

        private OpenAIResponse ParseOpenAIResponse(string responseString)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                string title = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").ToString();
                string author = "Unknown";

                var keywords = new Dictionary<string, int>();
                keywords["Research"] = 10;

                return new OpenAIResponse { Title = title, Author = author, Keywords = keywords };
            }
            catch
            {
                return new OpenAIResponse { Title = "Error", Author = "Error", Keywords = new Dictionary<string, int>() };
            }
        }
    }

    public class OpenAIResponse
    {
        public string Title { get; set; } = "Unknown Title";
        public string Author { get; set; } = "Unknown Author";
        public Dictionary<string, int> Keywords { get; set; } = new Dictionary<string, int>();
    }
}
