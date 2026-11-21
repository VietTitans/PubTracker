import { detectSourceLabel } from "../features/publicationSources/publicationSources";

export default function SourceBadge({ url }: { url: string }) {
  const source = detectSourceLabel(url);
  return <span className={`badge badge-${source.toLowerCase()}`}>{source}</span>;
}
