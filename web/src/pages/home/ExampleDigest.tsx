import { exampleDigest, exampleRecordCount } from "./digestSample";

const VISIBLE_TOPICS = 2;

export default function ExampleDigest() {
  return (
    <div className="example-digest">
      <div className="example-digest-back" aria-hidden="true" />
      <aside className="example-digest-sheet" aria-label="Example email digest">
        <span className="home-hero-tag">Example email digest</span>
        <p className="example-digest-head">
          🧠 {exampleDigest.source} update for: <strong>{exampleDigest.category}</strong>
          <br />
          📈 New records: <strong>{exampleRecordCount}</strong>
        </p>
        {exampleDigest.topics.slice(0, VISIBLE_TOPICS).map((topic) => (
          <div className="example-digest-topic" key={topic.name}>
            <h4>
              {topic.name} ({topic.records.length})
            </h4>
            <ol>
              {topic.records.map((record) => (
                <li key={record.title}>
                  {record.title}
                  <i>Authors intent: {record.intention}</i>
                </li>
              ))}
            </ol>
          </div>
        ))}
        <div className="example-digest-fade" />
      </aside>
    </div>
  );
}
