import { Link } from "react-router-dom";

// TODO(owner): fill in the three values below before launch. Review the text; this is not legal advice.
const CONTROLLER = "[Controller name and address]";
const PRIVACY_EMAIL = "privacy@example.org";
const VM_REGION = "[Oracle Cloud region]";

export function PrivacyPage() {
  return (
    <section className="card page-card">
      <h2>Privacy policy</h2>

      <h3>Who is responsible</h3>
      <p>
        The data controller is {CONTROLLER}. Contact: <a href={`mailto:${PRIVACY_EMAIL}`}>{PRIVACY_EMAIL}</a>.
      </p>

      <h3>What we store</h3>
      <p>
        Your sign-in details (email, name), the search URLs you subscribe to, and the public article records
        those searches return.
      </p>

      <h3>Who processes your data</h3>
      <ul>
        <li>Brevo (EU): sends our emails, including Digests and sign-in mails.</li>
        <li>Cloudflare: network proxy and tunnel in front of the site.</li>
        <li>Oracle Cloud ({VM_REGION}): hosts the app and database.</li>
        <li>Cloudflare R2 (EU): stores encrypted nightly database backups.</li>
        <li>Sentry (EU): receives error reports, with personal data scrubbed.</li>
        <li>Anthropic (LLM provider): writes the AI summaries, see below.</li>
      </ul>

      <h3>What is sent to the AI provider</h3>
      <p>
        Only the titles and abstracts of public articles. We send no user identifiers, email addresses or
        search query text.
      </p>

      <h3>How long we keep it</h3>
      <p>
        When you delete your account, it is kept for a 30-day grace period in which you can recover it by
        signing in. After that it is deleted. Backups are kept for 14 days, so your data can persist for up to
        about 44 days after you ask for deletion.
      </p>

      <h3>Getting a copy of your data</h3>
      <p>
        Email <a href={`mailto:${PRIVACY_EMAIL}`}>{PRIVACY_EMAIL}</a>. We answer within 30 days.
      </p>
    </section>
  );
}

export function TermsPage() {
  return (
    <section className="card page-card">
      <h2>Terms of use</h2>
      <p>
        PubTracker emails you new results from the literature searches you subscribe to. It is provided as is,
        with no guarantee that every new article is found or that AI summaries are accurate. Check the
        original article before relying on a summary.
      </p>
      <p>
        Use the service lawfully and do not try to disrupt it. You can stop all Digests or delete your account
        at any time. Questions: <a href={`mailto:${PRIVACY_EMAIL}`}>{PRIVACY_EMAIL}</a>. See the{" "}
        <Link to="/privacy">privacy policy</Link>.
      </p>
    </section>
  );
}
