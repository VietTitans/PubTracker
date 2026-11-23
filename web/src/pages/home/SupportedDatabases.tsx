import { supportedPublicationSources } from "./publicationSources";

export default function SupportedDatabases() {
  return (
    <div className="supported-databases">
      <span className="supported-databases-label">Supported databases</span>
      <ul>
        {supportedPublicationSources.map((source) => (
          <li key={source.label}>
            <a
              href={source.homepageUrl}
              target="_blank"
              rel="noreferrer"
              className={`badge badge-${source.label.toLowerCase()}`}
              title={`Open ${source.label} in a new tab`}
            >
              {source.label}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}
