-- Sources that provide research papers
CREATE TABLE IF NOT EXISTS sources (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE
);

-- Unique papers (deduplicated by DOI)
CREATE TABLE papers (
    id SERIAL PRIMARY KEY,
    doi VARCHAR(255) UNIQUE NOT NULL,
    title TEXT NOT NULL,
    abstract TEXT,
    url TEXT NOT NULL,
    date_first_discovered TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Track which sources have which papers
CREATE TABLE records (
    id SERIAL PRIMARY KEY,
    paper_id INT NOT NULL REFERENCES papers(id) ON DELETE CASCADE,
    source_id INT NOT NULL REFERENCES sources(id) ON DELETE CASCADE,
    external_id VARCHAR(255) NOT NULL,
    date_discovered TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (source_id, external_id),
    UNIQUE (paper_id, source_id)
);

-- Users
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(255) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);


-- Users who can subscribe
CREATE TABLE subscribers (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- User-specific saved searches (private to each user)
CREATE TABLE saved_searches (
    id SERIAL PRIMARY KEY,
    subscriber_id INT NOT NULL REFERENCES subscribers(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    query_params JSONB NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (subscriber_id, )
);

CREATE TABLE subscriber_saved_search_subscriptions (
    subscriber_id INT NOT NULL REFERENCES subscribers(id) ON DELETE CASCADE,
    saved_search_id INT NOT NULL REFERENCES saved_searches(id) ON DELETE CASCADE,
    PRIMARY KEY (subscriber_id, saved_search_id)
);

-- Notifications sent to subscribers
CREATE TABLE notifications (
    id SERIAL PRIMARY KEY,
    subscriber_id INT NOT NULL REFERENCES subscribers(id) ON DELETE CASCADE,
    record_id INT NOT NULL REFERENCES records(id) ON DELETE CASCADE,
    search_id INT NOT NULL REFERENCES saved_searches(id) ON DELETE CASCADE,
    sent_at TIMESTAMP NOT NULL DEFAULT now(),
    UNIQUE (subscriber_id, record_id, search_id)
);


-- On User deletetion resign saved_searches to placeholderUser
CREATE OR REPLACE FUNCTION reassign_deleted_user_searches()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE saved_searches
    SET subscriber_id = -1  -- placeholderUser
    WHERE subscriber_id = OLD.id;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_reassign_saved_searches
BEFORE DELETE ON subscribers
FOR EACH ROW
EXECUTE FUNCTION reassign_deleted_user_searches();


-- Indexes for performance
CREATE INDEX idx_records_paper_id ON records(paper_id);
CREATE INDEX idx_records_source_id ON records(source_id);
CREATE INDEX IF NOT EXISTS idx_papers_doi ON papers(doi);
CREATE INDEX idx_papers_date_discovered ON papers(date_first_discovered);
CREATE INDEX idx_saved_searches_subscriber ON saved_searches(subscriber_id);
CREATE INDEX idx_saved_searches_active ON saved_searches(subscriber_id, is_active);
CREATE INDEX idx_saved_searches_query_params ON saved_searches USING gin(query_params);
CREATE INDEX idx_notifications_subscriber ON notifications(subscriber_id);
CREATE INDEX idx_notifications_record ON notifications(record_id);
CREATE INDEX idx_notifications_search ON notifications(search_id);
CREATE INDEX idx_subscriber_subscriptions ON subscriber_saved_search_subscriptions(subscriber_id);
CREATE INDEX idx_saved_search_subscriptions ON subscriber_saved_search_subscriptions(saved_search_id);
