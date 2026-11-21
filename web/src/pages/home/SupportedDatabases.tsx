import { Link } from "react-router-dom";
import { supportedSources } from "../../lib/sources";

export default function SupportedDatabases() {
  return (
    <div className="supported-databases">
      <span className="supported-databases-label">Supported databases</span>
      <ul>
        {supportedSources.map((source) => (
          <li key={source.label} className={`badge badge-${source.label.toLowerCase()}`}>
            {source.label}
          </li>
        ))}
      </ul>
      <span className="supported-databases-more">
        More coming. <Link to="/suggest">Suggest a database</Link>
      </span>
    </div>
  );
}
