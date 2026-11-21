import { Link } from "react-router-dom";
import SearchBox from "../../features/subscriptions/SearchBox";
import ExampleDigest from "./ExampleDigest";
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
          Paste a PubMed search link. We check it weekly and email you only the new papers.
        </p>
        <div>
          <SearchBox
            isSubmitting={false}
            onSubmit={onSubmit}
            submitLabel="Start tracking"
            placeholder="Paste a PubMed search link"
          />
        </div>
        <p className="home-hero-links">
          <Link to="/how-it-works">See how it works →</Link>
          <span>
            <b>PubMed</b> supported · more coming. <Link to="/suggest">Suggest a database</Link>
          </span>
        </p>
      </div>
      <ExampleDigest />
    </section>
  );
}
