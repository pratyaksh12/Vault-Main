"use client";

import { useState } from "react";
import axios from "axios";

// Define the Document Interface matching the Backend SearchResult
interface DocumentResult {
  id: string;
  path: string;
  snippet: string;
  pageNumber: number; // Added Page Number
}

export default function Home() {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<DocumentResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query) return;

    setLoading(true);
    setError("");
    setResults([]);

    try {
      const response = await axios.get<DocumentResult[]>(
        `http://localhost:5123/api/documents/search?query=${query}`
      );
      setResults(response.data);
    } catch (err: any) {
      setError(`Error: ${err.message || "Unknown error"}. Check Console for details.`);
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files?.[0]) return;
    
    const file = e.target.files[0];
    const formData = new FormData();
    formData.append("file", file);

    try {
        await axios.post("http://localhost:5123/api/documents/upload", formData);
        alert(`Successfully uploaded: ${file.name}\nIt will be indexed shortly.`);
    } catch (err: any) {
        console.error(err);
        alert(`Upload failed: ${err.message}`);
    }
  };

  const handleOpen = async (id: string) => {
    try {
        await axios.get(`http://localhost:5123/api/documents/${id}/open`);
    } catch (err: any) {
        console.error(err);
        alert("Failed to open file locally. Is the backend running?");
    }
  };

  return (
    <main className="min-h-screen bg-neutral-900 text-neutral-100 p-8">
      <div className="max-w-4xl mx-auto space-y-8">
        
        {/* Header & Upload */}
        <div className="flex flex-col items-center space-y-4 relative">
          <h1 className="text-5xl font-bold bg-gradient-to-r from-blue-400 to-purple-500 bg-clip-text text-transparent">
            Vault Search
          </h1>
          <p className="text-neutral-400">Search your indexed documents instantly.</p>
          
          {/* Upload Button */}
          <div className="absolute top-0 right-0">
            <label className="bg-green-600 hover:bg-green-700 text-white text-sm font-medium px-4 py-2 rounded-lg cursor-pointer transition-colors shadow-lg shadow-green-900/20 flex items-center gap-2">
              <span>Upload File</span>
              <input type="file" className="hidden" onChange={handleUpload} accept=".pdf,.zip,.txt" />
            </label>
          </div>
        </div>

        {/* Search Bar */}
        <form onSubmit={handleSearch} className="flex gap-4">
          <input
            type="text"
            className="flex-1 bg-neutral-800 border border-neutral-700 rounded-lg px-4 py-3 focus:ring-2 focus:ring-blue-500 outline-none text-lg transition-all"
            placeholder="Search for keywords (e.g., 'pdf', 'invoice')..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
          <button
            type="submit"
            disabled={loading}
            className="bg-blue-600 hover:bg-blue-700 text-white font-medium px-8 py-3 rounded-lg transition-colors disabled:opacity-50"
          >
            {loading ? "Searching..." : "Search"}
          </button>
        </form>

        {/* Error Message */}
        {error && (
          <div className="p-4 bg-red-900/50 border border-red-800 rounded-lg text-red-200">
            {error}
          </div>
        )}

        {/* Results Grid */}
        <div className="grid gap-6">
          {results.length > 0 ? (
            results.map((doc) => (
              <div
                key={doc.id}
                onClick={() => handleOpen(doc.id)}
                className="bg-neutral-800 border border-neutral-700 p-6 rounded-xl hover:border-blue-500 cursor-pointer transition-all active:scale-[0.99]"
              >
                <div className="flex items-center justify-between mb-2">
                    <span className="text-xs font-mono text-neutral-500 truncate flex-1 min-w-0 mr-4" title={doc.path}>
                        {doc.path}
                    </span>
                    
                    {/* Page Badge */}
                    <span className="text-xs bg-blue-900/50 text-blue-200 px-2 py-1 rounded mr-2 shrink-0">
                        Page {doc.pageNumber}
                    </span>

                    <span className="text-xs bg-neutral-700 px-2 py-1 rounded text-neutral-300 shrink-0">
                        Match
                    </span>
                </div>
                
                {/* Content Snippet (Rendered HTML for Highlights) */}
                <p 
                    className="text-neutral-300 leading-relaxed break-all"
                    dangerouslySetInnerHTML={{ __html: doc.snippet }}
                />
              </div>
            ))
          ) : (
            !loading && results.length === 0 && query && (
                <div className="text-center text-neutral-500 py-10">No results found.</div>
            )
          )}
        </div>

      </div>
    </main>
  );
}
