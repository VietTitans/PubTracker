-- Backfill per-user digest watermarks for subscriptions that existed before
-- user_search_query_digests was introduced, seeded from the old query-wide watermark so
-- already-sent records aren't resent and already-pending-but-unsent records stay pending.
-- No-op on a fresh database (nothing in user_search_queries yet).
INSERT INTO user_search_query_digests (user_id, search_query_id, last_digest_sent_at)
SELECT usq.user_id, usq.search_query_id, sq.last_digest_sent_at
FROM user_search_queries usq
JOIN search_queries sq ON sq.id = usq.search_query_id
ON CONFLICT (user_id, search_query_id) DO NOTHING;
