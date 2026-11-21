import { Link } from "react-router-dom";
import SearchBox from "../../features/subscriptions/SearchBox";
import ExampleDigest from "./ExampleDigest";
import SupportedDatabases from "./SupportedDatabases";
import "./HomeHero.css";

export default function HomeHero({ onSubmit }: { onSubmit: (url: string) => Promise<boolean> }) {
  return (
    <section className="home-hero">
      <div className="home-hero-copy">
        <span className="home-hero-pill">For clinicians & researchers</span>
        <h1>
          Stop re&#8209;running the <span className="home-hero-accent">same search</span> every week.
        </h1>
        <p className="home-hero-lead">
          Paste a search link from your library database. We check it weekly and email you only the new papers.
        </p>
        <div>
          <SearchBox
            isSubmitting={false}
            onSubmit={onSubmit}
            submitLabel="Start tracking"
            placeholder="Paste a search link"
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
