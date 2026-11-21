import type { SearchQuery } from "../../api/client";
import Spinner from "../../components/Spinner";
import { detectSourceLabel } from "../../lib/sourceLabel";

// Spinner until the backend has counted the Search Query's Records, unless its last poll failed.
export default function RecordCount({ searchQuery }: { searchQuery: SearchQuery }) {
  const { sourceRecordCount, lastPollFailedAt, targetUrl } = searchQuery;
  return (
    <div className="record-count" title="Records registered">
      <span className="record-count-label">Records</span>
      <span className="record-count-row">
        {sourceRecordCount === null && !lastPollFailedAt ? (
          <Spinner />
        ) : (
          <span className="record-count-value">{sourceRecordCount ?? "—"}</span>
        )}
        {lastPollFailedAt && (
          <span
            className="record-count-status"
            title={`Couldn't reach ${detectSourceLabel(targetUrl)} on the last check - will retry on the next scheduled check`}
          >
            ⚠
          </span>
        )}
      </span>
    </div>
  );
}
