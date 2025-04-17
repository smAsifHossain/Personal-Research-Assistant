import fitz  # PyMuPDF for extracting text
import nltk
import spacy
from collections import Counter
from sklearn.feature_extraction.text import TfidfVectorizer

# Download necessary NLTK resources
nltk.download("stopwords")
nltk.download("punkt")

# Load spaCy NLP model
nlp = spacy.load("en_core_web_sm")

# Stopwords for filtering common words
stopwords = set(nltk.corpus.stopwords.words("english"))

def extract_text_from_pdf(file_path):
    """Extract text and metadata from a PDF file."""
    doc = fitz.open(file_path)
    text = " ".join(page.get_text("text") for page in doc)
    metadata = doc.metadata

    title = metadata.get("title", "Unknown")
    author = metadata.get("author", "Unknown")
    
    return title, author, text

def clean_and_tokenize(text):
    """Tokenize text, remove stopwords, and apply lemmatization."""
    doc = nlp(text.lower())
    tokens = [token.lemma_ for token in doc if token.text.isalpha() and token.text not in stopwords]
    return tokens

def get_top_keywords(text, top_n=10):
    """Extract top keywords using TF-IDF."""
    vectorizer = TfidfVectorizer(stop_words="english", max_features=top_n)
    tfidf_matrix = vectorizer.fit_transform([text])
    feature_names = vectorizer.get_feature_names_out()
    
    tfidf_scores = tfidf_matrix.toarray()[0]
    keyword_freq = {feature_names[i]: round(tfidf_scores[i], 4) for i in range(len(feature_names))}
    return dict(sorted(keyword_freq.items(), key=lambda item: item[1], reverse=True))

def process_pdf(file_path):
    """Extract text, metadata, and keywords from a PDF."""
    title, author, text = extract_text_from_pdf(file_path)
    cleaned_tokens = clean_and_tokenize(text)
    keyword_frequency = get_top_keywords(" ".join(cleaned_tokens))

    return {
        "title": title,
        "author": author,
        "text": text,
        "keywords": keyword_frequency
    }
