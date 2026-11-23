import { Link } from "react-router-dom";
import SearchBox from "../../features/subscriptions/SearchBox";
import ExampleDigest from "./ExampleDigest";
import SupportedDatabases from "./SupportedDatabases";
import { supportedPublicationSources } from "./publicationSources";
import "./HomeHero.css";

const EXAMPLE_SEARCH_URLS = supportedPublicationSources.flatMap((source) => source.exampleSearchUrl ?? []);

export default function HomeHero({ onSubmit }: { onSubmit: (url: string) => Promise<boolean> }) {
  return (
    <section className="home-hero">
      <div className="home-hero-copy">
        <span className="home-hero-pill">For clinicians & researchers</span>
        <h1>
          Stay on top of the <span className="home-hero-accent">latest research</span> in your field.
        </h1>
        <p className="home-hero-lead">
          PubTracker monitors and emails you the new research. You decide what to monitor.
        </p>
        <div>
          <SearchBox
            isSubmitting={false}
            onSubmit={onSubmit}
            submitLabel="Start tracking"
            placeholder="Paste a search link to watch"
            placeholderExamples={EXAMPLE_SEARCH_URLS}
          />
        </div>
        <SupportedDatabases />
        <p className="home-hero-links">
          <Link to="/how-it-works">See how it works →</Link>
        </p>
      </div>
      <ExampleDigest />
    </section>
  );
}
