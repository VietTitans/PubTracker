import { Link } from "react-router-dom";
import SearchBox from "../features/subscriptions/SearchBox";
import SubscriptionList from "../features/subscriptions/SubscriptionList";
import { rememberPendingUrl, type Subscriptions } from "../features/subscriptions/useSubscriptions";

export default function HomePage({
  isAuthenticated,
  subscriptions,
  onLogin,
}: {
  isAuthenticated: boolean;
  subscriptions: Subscriptions;
  onLogin: () => void;
}) {
  if (isAuthenticated) {
    return (
      <>
        <section className="hero hero-compact">
          <SearchBox isSubmitting={subscriptions.isSubmitting} onSubmit={subscriptions.subscribe} />
        </section>
        <SubscriptionList subscriptions={subscriptions} />
      </>
    );
  }

  async function handleSignedOutSearch(url: string) {
    rememberPendingUrl(url);
    onLogin();
    return false;
  }

  return (
    <section className="hero">
      <h2>Track new research. Get a digest.</h2>
      <SearchBox isSubmitting={false} onSubmit={handleSignedOutSearch} />
      <ol className="steps">
        <li>
          <strong>1</strong>Paste a search URL
        </li>
        <li>
          <strong>2</strong>We check it on a schedule
        </li>
        <li>
          <strong>3</strong>New results land in your inbox
        </li>
      </ol>
      <p className="supported">
        <span className="badge badge-pubmed">PubMed</span>
        <span>
          More databases coming. <Link to="/suggest">Suggest one</Link>.
        </span>
      </p>
    </section>
  );
}
