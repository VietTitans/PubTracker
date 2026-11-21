import type { SearchQuery } from "../../api/client";
import Modal from "../../components/Modal";
import SourceBadge from "../../components/SourceBadge";
import Spinner from "../../components/Spinner";
import TagList from "../../components/TagList";

export default function DetailDialog({
  target,
  detail,
  isLoading,
  error,
  onClose,
  onRequestUnsubscribe,
}: {
  target: SearchQuery;
  detail: SearchQuery | null;
  isLoading: boolean;
  error: string | null;
  onClose: () => void;
  onRequestUnsubscribe: () => void;
}) {
  const lastFetch = detail?.lastFetchedAt
    ? new Date(detail.lastFetchedAt).toLocaleString(undefined, { hour12: false })
    : "Never";
  const lastPollFailedAt = detail?.lastPollFailedAt
    ? new Date(detail.lastPollFailedAt).toLocaleString(undefined, { hour12: false })
    : null;
  const keywords = detail?.tags ?? [];

  return (
    <Modal titleId="detail-title" onClose={onClose}>
      <div className="detail-header">
        <SourceBadge url={target.targetUrl} />
        <h3 id="detail-title">Search details</h3>
      </div>

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}

      <dl className="detail-list">
        <div className="detail-tags-row">
          <dt>Keywords</dt>
          <dd>
            {isLoading ? (
              <Spinner />
            ) : keywords.length > 0 ? (
              <TagList tags={keywords} />
            ) : (
              "—"
            )}
          </dd>
        </div>
        <div>
          <dt>Records registered</dt>
          <dd>{isLoading ? <Spinner /> : (detail?.sourceRecordCount ?? "—")}</dd>
        </div>
        <div>
          <dt>Last fetch</dt>
          <dd>{isLoading ? <Spinner /> : lastFetch}</dd>
        </div>
        {!isLoading && lastPollFailedAt && (
          <div>
            <dt>Status</dt>
            <dd className="detail-status-warning">
              Last check failed ({lastPollFailedAt}) - will retry on the next scheduled check
            </dd>
          </div>
        )}
      </dl>

      <a href={target.targetUrl} target="_blank" rel="noreferrer" className="detail-url">
        Click here to follow the URL
      </a>

      <div className="modal-actions">
        <button className="btn btn-danger-outline" onClick={onRequestUnsubscribe}>
          Unsubscribe
        </button>
        <button className="btn btn-ghost" onClick={onClose} autoFocus>
          Close
        </button>
      </div>
    </Modal>
  );
}
