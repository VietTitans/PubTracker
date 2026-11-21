import SearchBox from "../features/subscriptions/SearchBox";
import SubscriptionList from "../features/subscriptions/SubscriptionList";
import { rememberPendingUrl, type Subscriptions } from "../features/subscriptions/useSubscriptions";
import HomeHero from "./home/HomeHero";

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

  return <HomeHero onSubmit={handleSignedOutSearch} />;
}
