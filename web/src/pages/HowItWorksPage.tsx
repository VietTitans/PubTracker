import { Link } from "react-router-dom";

const STEPS = [
  {
    title: "Do a literature search",
    body: "Search as you normally would. Copy the URL from the address bar in the database.",
  },
  {
    title: "Paste it and subscribe",
    body: "Paste the URL in PubTracker's search box.",
  },
  {
    title: "Done!",
    body: "We track your search queries and send you an email when new records are found.",
  },
];

const EXAMPLE_GROUPS = [
  {
    name: "Systematic reviews & meta-analyses",
    records: [
      { title: "Exercise therapy for chronic low back pain: a systematic review and meta-analysis", intent: "To compare exercise types on pain and disability outcomes." },
    ],
  },
  {
    name: "Exercise & movement-based therapy",
    records: [
      { title: "Effects of a 12-week core stability exercise programme in adults with low back pain", intent: "To test whether core stability reduces pain more than usual care." },
      { title: "Yoga versus physical therapy for persistent lower back pain: a randomized trial", intent: null },
    ],
  },
];

export default function HowItWorksPage() {
  return (
    <>
      <section className="card page-card">
        <h2>How it works</h2>
        <p className="page-hint">Stop re-running the same literature search by hand. PubTracker does it for you.</p>
        <ol className="how-steps">
          {STEPS.map((step, i) => (
            <li key={step.title}>
              <span className="how-step-num">{i + 1}</span>
              <div>
                <strong>{step.title}</strong>
                <p>{step.body}</p>
              </div>
            </li>
          ))}
        </ol>
      </section>

      <section className="card page-card">
        <h2>What the email looks like</h2>
        <p className="page-hint">An example digest. The papers below are made up for illustration.</p>
        <div className="digest-example">
          <p className="digest-subject">
            <span>Subject:</span> 3 new records for your search
          </p>
          <div className="digest-body">
            <p className="digest-meta">
              🧠 Database update for: <strong>Lumbar spine, SIJ or pelvis</strong>
              <br />
              📈 Number of new records: <strong>3</strong>
              <br />
              🔗 Search URL: <a href="#" onClick={(e) => e.preventDefault()}>Link</a>
            </p>
            <div className="digest-summary">
              <h4>🤖 AI Summary</h4>
              <p>
                Three new studies on exercise for low back pain: one review pooling trial data, plus two trials of core
                stability and yoga compared with usual care and physical therapy.
              </p>
            </div>
            {EXAMPLE_GROUPS.map((group) => (
              <div className="digest-group" key={group.name}>
                <h4>
                  {group.name} ({group.records.length})
                </h4>
                <ul>
                  {group.records.map((r, i) => (
                    <li key={r.title}>
                      <span className="digest-num">{i + 1}.</span>
                      <a href="#" onClick={(e) => e.preventDefault()}>
                        {r.title}
                      </a>
                      {r.intent && <div className="digest-intent">Authors intent: {r.intent}</div>}
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </div>
      </section>
    </>
  );
}
