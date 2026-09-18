export function detectSourceLabel(url: string): string {
  const lower = url.toLowerCase();
  if (lower.includes("pedro")) return "PEDro";
  if (lower.includes("pubmed")) return "PubMed";
  return "Other";
}
