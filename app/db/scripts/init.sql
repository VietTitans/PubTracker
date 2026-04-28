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


-- User-specific saved searches (private to each user)
CREATE TABLE saved_searches (
    id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    query_params JSONB NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id)
);


-- Indexes for performance
CREATE INDEX idx_records_paper_id ON records(paper_id);
CREATE INDEX idx_records_source_id ON records(source_id);
CREATE INDEX IF NOT EXISTS idx_papers_doi ON papers(doi);
CREATE INDEX idx_papers_date_discovered ON papers(date_first_discovered);
CREATE INDEX idx_saved_searches_active ON saved_searches(user_id, is_active);
CREATE INDEX idx_saved_searches_query_params ON saved_searches USING gin(query_params);
