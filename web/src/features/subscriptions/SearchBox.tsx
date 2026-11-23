import { useState } from "react";
import Spinner from "../../components/Spinner";
import { useTypingPlaceholder } from "./useTypingPlaceholder";

// onSubmit resolves true when the URL was accepted, which clears the input.
export default function SearchBox({
  isSubmitting,
  onSubmit,
  submitLabel = "Subscribe",
  placeholder = "Paste a search URL",
  placeholderExamples,
}: {
  isSubmitting: boolean;
  onSubmit: (url: string) => Promise<boolean>;
  submitLabel?: string;
  placeholder?: string;
  // When given, the empty box types these out one after another instead of showing `placeholder`,
  // until it is focused or filled in. Should be a stable array.
  placeholderExamples?: string[];
}) {
  const [targetUrl, setTargetUrl] = useState("");
  const [isFocused, setIsFocused] = useState(false);
  const isIdle = !isFocused && targetUrl === "";
  const typedExample = useTypingPlaceholder(placeholderExamples ?? [], !isIdle);
  const shownPlaceholder = placeholderExamples && isIdle ? typedExample : placeholder;

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
        placeholder={shownPlaceholder}
        aria-label="Search URL"
        value={targetUrl}
        onChange={(e) => setTargetUrl(e.target.value)}
        onFocus={() => setIsFocused(true)}
        onBlur={() => setIsFocused(false)}
        required
      />
      <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
        {isSubmitting ? <Spinner /> : submitLabel}
      </button>
    </form>
  );
}
