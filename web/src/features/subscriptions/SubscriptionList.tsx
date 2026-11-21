import { useState } from "react";
import type { SearchQuery } from "../../api/client";
import ConfirmDialog from "../../components/ConfirmDialog";
import Spinner from "../../components/Spinner";
import DetailDialog from "./DetailDialog";
import SubscriptionRow from "./SubscriptionRow";
import type { Subscriptions } from "./useSubscriptions";

export default function SubscriptionList({ subscriptions }: { subscriptions: Subscriptions }) {
  const {
    searchQueries,
    unsubscribingId,
    isRedoing,
    canRedo,
    detail,
    unsubscribe,
    redoUnsubscribe,
    openDetail,
    closeDetail,
  } = subscriptions;
  const [confirmTarget, setConfirmTarget] = useState<SearchQuery | null>(null);

  async function handleConfirmUnsubscribe() {
    if (confirmTarget && (await unsubscribe(confirmTarget))) {
      setConfirmTarget(null);
    }
  }

  return (
    <>
      <section>
        <div className="section-header">
          <h2>Your search queries</h2>
          {searchQueries.length > 0 && <span className="count-badge">{searchQueries.length}</span>}
          {canRedo && (
            <button className="btn btn-ghost btn-sm section-header-action" onClick={redoUnsubscribe} disabled={isRedoing}>
              {isRedoing ? <Spinner /> : "Redo"}
            </button>
          )}
        </div>

        {searchQueries.length === 0 ? (
          <div className="empty-state">
            <p>You're not tracking any searches yet.</p>
            <p className="empty-state-hint">
              Paste a PubMed search link above and we'll email you a digest when new papers appear.
            </p>
          </div>
        ) : (
          <ul className="search-query-list">
            {searchQueries.map((sq) => (
              <SubscriptionRow
                key={sq.id}
                searchQuery={sq}
                isUnsubscribing={unsubscribingId === sq.id}
                onOpen={() => openDetail(sq)}
                onUnsubscribe={() => setConfirmTarget(sq)}
              />
            ))}
          </ul>
        )}
      </section>

      {confirmTarget && (
        <ConfirmDialog
          title="Unsubscribe from this search?"
          message={confirmTarget.targetUrl}
          confirmLabel="Unsubscribe"
          isBusy={unsubscribingId === confirmTarget.id}
          onConfirm={handleConfirmUnsubscribe}
          onCancel={() => setConfirmTarget(null)}
        />
      )}

      {detail && (
        <DetailDialog
          target={detail.target}
          detail={detail.data}
          isLoading={detail.isLoading}
          error={detail.error}
          onClose={closeDetail}
          onRequestUnsubscribe={() => {
            setConfirmTarget(detail.target);
            closeDetail();
          }}
        />
      )}
    </>
  );
}
