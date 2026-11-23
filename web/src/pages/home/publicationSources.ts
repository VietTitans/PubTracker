export interface PublicationSource {
  label: string;
  // The database's own website, linked from the home page.
  homepageUrl: string;
  // A valid search link from this database, typed out in the home page's paste box.
  exampleSearchUrl?: string;
  // Lower-case text that appears in a search URL from this database.
  urlMarker: string;
  // Listed as supported on the home page. Mirrors the API's SourceCatalog (Sources:<Name>:Enabled).
  supported: boolean;
}

// The one list of databases the web app knows about: add a row here to list a new database on the
// home page and to recognise its links. Optionally give it a colour with a `.badge-<label>` rule in App.css.
export const PUBLICATION_SOURCES: PublicationSource[] = [
  {
    label: "PubMed",
    homepageUrl: "https://pubmed.ncbi.nlm.nih.gov/",
    exampleSearchUrl: "https://pubmed.ncbi.nlm.nih.gov/?term=pain",
    urlMarker: "pubmed",
    supported: true,
  },
  { label: "PEDro", homepageUrl: "https://pedro.org.au/", urlMarker: "pedro", supported: false },
];

export const supportedPublicationSources = PUBLICATION_SOURCES.filter((source) => source.supported);

export function detectSourceLabel(url: string): string {
  const lower = url.toLowerCase();
  return PUBLICATION_SOURCES.find((source) => lower.includes(source.urlMarker))?.label ?? "Other";
}
