using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalResearchAssistantV5.Data;
using PersonalResearchAssistantV5.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.IO;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text.RegularExpressions;

namespace PersonalResearchAssistantV5.Controllers
{
    [Authorize] // Require authentication for all actions unless overridden
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public HomeController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // Landing Page
        public IActionResult Index()
        {
            return View();
        }

        // User Home Page: Only accessible by users with role "User"
        [Authorize(Roles = "User")]
        public async Task<IActionResult> UserHome()
        {
            var user = await _userManager.GetUserAsync(User);
            var uploadedPdfs = await _context.UserPdfs
                .Where(p => p.UserId == user.Id)
                .ToListAsync();

            var model = new UserHomeViewModel
            {
                User = user,
                UploadedPdfs = uploadedPdfs
            };

            return View(model);
        }

        // Admin Home Page: Only accessible by Admins
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminHome()
        {
            var allUsers = await _userManager.Users.ToListAsync();
            var pendingUsers = new List<ApplicationUser>();
            var approvedUsers = new List<ApplicationUser>();
            var uploadedPdfs = await _context.UserPdfs.Include(p => p.User).ToListAsync();

            // Separate users with and without assigned roles
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Count == 0)
                {
                    pendingUsers.Add(user);
                }
                else
                {
                    approvedUsers.Add(user);
                }
            }

            var model = new AdminHomeViewModel
            {
                PendingUsers = pendingUsers,
                ApprovedUsers = approvedUsers,
                UploadedPdfs = uploadedPdfs
            };

            return View(model);
        }

        // Approve a user by assigning them a role
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("Invalid user ID.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            string role = user.UserType == "Admin" ? "Admin" : "User";
            await _userManager.AddToRoleAsync(user, role);

            return RedirectToAction("AdminHome", "Home");
        }

        // Decline a user by removing them from the system
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeclineUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("Invalid user ID.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return RedirectToAction("AdminHome", "Home");
        }

        // Delete an approved user
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("Invalid user ID.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest("Failed to delete user.");
            }

            return RedirectToAction("AdminHome", "Home");
        }

        // Extract Text and Metadata from a PDF
        public IActionResult ExtractText(int id)
        {
            var pdf = _context.UserPdfs.FirstOrDefault(p => p.Id == id);
            if (pdf == null || string.IsNullOrEmpty(pdf.FilePath))
            {
                TempData["Error"] = "PDF file not found!";
                return RedirectToAction("UserHome");
            }

            var (extractedText, title, author, publicationDate) = ExtractTextFromPdf(pdf.FilePath);
            var keywordAnalysis = PerformKeywordAnalysis(extractedText);

            var viewModel = new ExtractTextViewModel
            {
                FileName = pdf.FileName,
                ExtractedText = extractedText,
                KeywordFrequency = keywordAnalysis,
                Title = title,
                Author = author,
                PublicationDate = publicationDate
            };

            return View("ExtractedText", viewModel);
        }

        // Helper method: Extract raw text and metadata from PDF using iTextSharp
        private (string extractedText, string title, string author, string publicationDate) ExtractTextFromPdf(string filePath)
        {
            try
            {
                string fullPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath.TrimStart('/'));

                if (!System.IO.File.Exists(fullPath))
                {
                    return ($"Error: PDF file not found at {fullPath}", "Not Available", "Not Available", "Not Available");
                }

                using (PdfReader reader = new PdfReader(fullPath))
                {
                    string text = "";
                    string firstPageText = "";

                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        string pageText = PdfTextExtractor.GetTextFromPage(reader, i);
                        text += pageText + "\n\n";

                        if (i == 1) firstPageText = pageText;
                    }

                    var metadata = reader.Info;
                    string title = metadata.ContainsKey("Title") && !string.IsNullOrWhiteSpace(metadata["Title"]) ? metadata["Title"] : DetectTitle(firstPageText);
                    string author = metadata.ContainsKey("Author") && !string.IsNullOrWhiteSpace(metadata["Author"]) ? metadata["Author"] : DetectAuthor(firstPageText);
                    string publicationDate = metadata.ContainsKey("CreationDate") && !string.IsNullOrWhiteSpace(metadata["CreationDate"])
                        ? FormatPublicationDate(metadata["CreationDate"])
                        : "Not Available";

                    return (text, title, author, publicationDate);
                }
            }
            catch (Exception ex)
            {
                return ($"Error extracting text: {ex.Message}", "Not Available", "Not Available", "Not Available");
            }
        }

        // Guess title from first few lines
        private string DetectTitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Not Available";

            var lines = text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Take(5).ToList();
            return lines.Count > 0 ? lines[0].Trim() : "Not Available";
        }

        // Guess author from common keywords in first few lines
        private string DetectAuthor(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Not Available";

            var lines = text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Take(10).ToList();
            foreach (var line in lines)
            {
                if (line.ToLower().Contains("by ") || line.ToLower().Contains("author"))
                    return line.Replace("by", "", StringComparison.OrdinalIgnoreCase).Trim();
            }

            return "Not Available";
        }

        // Keyword analysis: count meaningful word frequencies
        private Dictionary<string, int> PerformKeywordAnalysis(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Dictionary<string, int> { { "No meaningful text found", 0 } };
            }

            var words = text
                .ToLower()
                .Split(new char[] { ' ', '\n', '\r', '.', ',', '!', '?', ';', ':', '(', ')', '[', ']', '{', '}' }, StringSplitOptions.RemoveEmptyEntries);

            var stopwords = new HashSet<string> { "the", "is", "in", "and", "to", "of", "a", "for", "on", "with", "as", "this", "that", "at", "it", "by", "from", "or", "an" };

            var wordCounts = words
                .Where(w => !stopwords.Contains(w) && w.Length > 3)
                .GroupBy(w => w)
                .ToDictionary(g => g.Key, g => g.Count());

            return wordCounts.Any()
                ? wordCounts.OrderByDescending(kvp => kvp.Value).Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                : new Dictionary<string, int> { { "No significant keywords found", 0 } };
        }

        // Format raw PDF creation date to yyyy-MM-dd
        private string FormatPublicationDate(string rawDate)
        {
            try
            {
                if (rawDate.StartsWith("D:") && rawDate.Length >= 10)
                {
                    return rawDate.Substring(2, 4) + "-" + rawDate.Substring(6, 2) + "-" + rawDate.Substring(8, 2);
                }
                return rawDate;
            }
            catch
            {
                return "Not Available";
            }
        }
    }
}
