import { useState } from "react";
import Spinner from "../../components/Spinner";

// onSubmit resolves true when the URL was accepted, which clears the input.
export default function SearchBox({
  isSubmitting,
  onSubmit,
  submitLabel = "Subscribe",
  placeholder = "Paste a search URL",
}: {
  isSubmitting: boolean;
  onSubmit: (url: string) => Promise<boolean>;
  submitLabel?: string;
  placeholder?: string;
}) {
  const [targetUrl, setTargetUrl] = useState("");

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const url = targetUrl.trim();
    if (!url) {
      return;
    }
    if (await onSubmit(url)) {
      setTargetUrl("");
    }
  }

  return (
    <form onSubmit={handleSubmit} className="search-box">
      <input
        id="target-url"
        type="url"
        placeholder={placeholder}
        aria-label="Search URL"
        value={targetUrl}
        onChange={(e) => setTargetUrl(e.target.value)}
        required
      />
      <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
        {isSubmitting ? <Spinner /> : submitLabel}
      </button>
    </form>
  );
}
