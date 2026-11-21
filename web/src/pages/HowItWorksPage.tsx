import { Link } from "react-router-dom";

const STEPS = [
  {
    title: "Paste a search link",
    body: "Run your search on a library database as usual, then copy the address from your browser and paste it into PubTracker.",
    example: "https://pubmed.ncbi.nlm.nih.gov/?term=low+back+pain+exercise",
  },
  {
    title: "We check it on a schedule",
    body: "PubTracker re-runs your search about once a week and compares the results with what it has already seen.",
  },
  {
    title: "Only new papers get through",
    body: "Records you have already been sent are never repeated, even if a database re-orders its results.",
  },
  {
    title: "One digest lands in your inbox",
    body: "New papers are grouped by topic. Switch on AI summaries to get a short plain-language read of each one.",
  },
];

export default function HowItWorksPage() {
  return (
    <section className="card page-card hiw">
      <p className="hiw-eyebrow">How it works</p>
      <h2>Stop re-running the same search every week.</h2>
      <p className="hiw-lead">
        Save a search from your database once. PubTracker watches it and emails you only what is new.
      </p>

      <ol className="hiw-timeline">
        {STEPS.map((step, i) => (
          <li key={step.title}>
            <span className="hiw-dot" aria-hidden="true">
              {i + 1}
            </span>
            <div className="hiw-body">
              <h3>{step.title}</h3>
              <p>{step.body}</p>
              {step.example && <code className="hiw-url">{step.example}</code>}
            </div>
          </li>
        ))}
      </ol>
      <Link className="btn btn-primary hiw-cta" to="/">
        Start tracking
      </Link>
    </section>
  );
}
