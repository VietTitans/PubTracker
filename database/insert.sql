-- 1. sources
INSERT INTO sources (id, name, base_url) VALUES
  (1, 'arXiv', 'https://arxiv.org'),
  (2, 'PubMed', 'https://pubmed.ncbi.nlm.nih.gov'),
  (3, 'IEEE Xplore', 'https://ieeexplore.ieee.org');

-- 2. search_queries
INSERT INTO search_queries (id, source_id, target_url, last_digest_sent_at) VALUES
  (1, 1, 'https://arxiv.org/search/?query=machine+learning&searchtype=all', '2026-07-15 09:00:00+00'),
  (2, 1, 'https://arxiv.org/search/?query=natural+language+processing&searchtype=all', '2026-07-20 14:30:00+00'),
  (3, 2, 'https://pubmed.ncbi.nlm.nih.gov/?term=genomics', NULL),
  (4, 3, 'https://ieeexplore.ieee.org/search/searchresult.jsp?queryText=robotics', '2026-07-18 18:45:00+00');

-- 3. records
INSERT INTO records (id, doi, title, description) VALUES
  (1, '10.1001/arxiv.2101.00001', 'A Survey of Machine Learning', 'Comprehensive review of machine learning techniques.'),
  (2, '10.1002/pubmed.123456', 'Genomics in Precision Medicine', 'Discussion of genomic methods for personalized treatments.'),
  (3, '10.1109/5.771073', 'Autonomous Robotics Systems', 'Overview of robotics systems with autonomous navigation.'),
  (4, '10.1001/arxiv.2202.00002', 'Advances in Natural Language Processing', 'New transformer architectures and benchmarks.');

-- 4. users
INSERT INTO users (id, name, username, email, is_marked_for_deletion, deletion_requested_at) VALUES
  (1, 'Alice Johnson', 'alicej', 'alice@example.com', FALSE, NULL),
  (2, 'Bob Martinez',  'bobm',   'bob@example.com',   FALSE, NULL);
  
-- 5. user_search_queries
INSERT INTO user_search_queries (id, user_id, search_query_id, created_at) VALUES
  (1, 1, 1, '2026-07-01 10:00:00+00'),
  (2, 1, 2, '2026-07-05 11:30:00+00'),
  (3, 2, 3, '2026-07-10 14:15:00+00'),
  (4, 2, 4, '2026-07-12 09:45:00+00');

-- 6. source_records
INSERT INTO source_records (id, record_id, source_id) VALUES
  (1, 1, 1),
  (2, 4, 1),
  (3, 2, 2),
  (4, 3, 3);

-- 7. search_query_records (Composite PK)
INSERT INTO search_query_records (search_query_id, record_id, first_seen_at) VALUES
  (1, 1, '2026-07-10 08:15:00+00'),
  (1, 4, '2026-07-20 14:00:00+00'),
  (2, 4, '2026-07-18 12:20:00+00'),
  (3, 2, '2026-07-16 11:05:00+00'),
  (4, 3, '2026-07-17 16:10:00+00'),
  (4, 1, '2026-07-19 09:30:00+00');

-- Reset identity sequence generators to avoid primary key conflicts on future INSERTs
SELECT setval(pg_get_serial_sequence('sources', 'id'), (SELECT MAX(id) FROM sources));
SELECT setval(pg_get_serial_sequence('search_queries', 'id'), (SELECT MAX(id) FROM search_queries));
SELECT setval(pg_get_serial_sequence('records', 'id'), (SELECT MAX(id) FROM records));
SELECT setval(pg_get_serial_sequence('users', 'id'), (SELECT MAX(id) FROM users));
SELECT setval(pg_get_serial_sequence('user_search_queries', 'id'), (SELECT MAX(id) FROM user_search_queries));
SELECT setval(pg_get_serial_sequence('source_records', 'id'), (SELECT MAX(id) FROM source_records));