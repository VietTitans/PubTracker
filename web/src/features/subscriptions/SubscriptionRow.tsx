import type { SearchQuery } from "../../api/client";
import Icon from "../../components/Icon";
import SourceBadge from "../../components/SourceBadge";
import Spinner from "../../components/Spinner";
import TagList from "../../components/TagList";
import RecordCount from "./RecordCount";

export default function SubscriptionRow({
  searchQuery,
  isUnsubscribing,
  onOpen,
  onUnsubscribe,
}: {
  searchQuery: SearchQuery;
  isUnsubscribing: boolean;
  onOpen: () => void;
  onUnsubscribe: () => void;
}) {
  return (
    <li onClick={onOpen} className="clickable">
      <SourceBadge url={searchQuery.targetUrl} />
      {searchQuery.tags.length > 0 ? (
        <TagList tags={searchQuery.tags} row />
      ) : (
        <span className="query-url-text" title={searchQuery.targetUrl}>
          {searchQuery.targetUrl}
        </span>
      )}
      <RecordCount searchQuery={searchQuery} />
      <a
        href={searchQuery.targetUrl}
        target="_blank"
        rel="noreferrer"
        className="btn btn-icon"
        onClick={(e) => e.stopPropagation()}
        aria-label="Open in new tab"
        title="Open in new tab"
      >
        <Icon name="externalLink" size={14} />
      </a>
      <button
        className="btn btn-icon btn-danger-hover"
        onClick={(e) => {
          e.stopPropagation();
          onUnsubscribe();
        }}
        disabled={isUnsubscribing}
        aria-label="Unsubscribe"
        title="Unsubscribe"
      >
        {isUnsubscribing ? <Spinner /> : <Icon name="trash" />}
      </button>
    </li>
  );
}
