import { useState } from "react";
import Spinner from "../../components/Spinner";

// onSubmit resolves true when the URL was accepted, which clears the input.
export default function SearchBox({
  isSubmitting,
  onSubmit,
}: {
  isSubmitting: boolean;
  onSubmit: (url: string) => Promise<boolean>;
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
        placeholder="Paste a search URL"
        aria-label="Search URL"
        value={targetUrl}
        onChange={(e) => setTargetUrl(e.target.value)}
        required
      />
      <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
        {isSubmitting ? <Spinner /> : "Subscribe"}
      </button>
    </form>
  );
}
