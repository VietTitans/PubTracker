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
          Stay on top of the <span className="home-hero-accent">latest research</span> in your field.
        </h1>
        <p className="home-hero-lead">
          Save the searches for the topics you follow. PubTracker keeps watch and emails you the new research that
          matches them, so you always know the state of the art.
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
