-- 1. sources
INSERT INTO sources (id, name, base_url) VALUES
  (1, 'arXiv', 'https://arxiv.org'),
  (2, 'PubMed', 'https://pubmed.ncbi.nlm.nih.gov'),
  (3, 'IEEE Xplore', 'https://ieeexplore.ieee.org'),
  (4, 'PEDro', 'https://search.pedro.org.au');

-- 2. search_queries
INSERT INTO search_queries (id, source_id, target_url, last_digest_sent_at) VALUES
  (1, 4, 'https://search.pedro.org.au/advanced-search/results?abstract_with_title=&therapy=VL01387&problem=VL01371&body_part=VL01396&subdiscipline=VL01359&topic=VL01402&method=0&authors_association=&title=&source=&year_of_publication=2020&date_record_was_created=&nscore=&perpage=20&lop=and&find=&find=Start+Search', NULL),
  (2, 4, 'https://search.pedro.org.au/advanced-search/results?abstract_with_title=&therapy=VL01387&problem=VL01371&body_part=VL01391&subdiscipline=VL01359&topic=VL01402&method=0&authors_association=&title=&source=&year_of_publication=2020&date_record_was_created=&nscore=&perpage=20&lop=and&find=&find=Start+Search', '2026-07-15 09:00:00+00'),
  (3, 4, 'https://search.pedro.org.au/advanced-search/results?abstract_with_title=ACL&therapy=VL01387&problem=VL01375&body_part=VL01399&subdiscipline=VL01361&topic=VL01406&method=0&authors_association=&title=&source=&year_of_publication=2020&date_record_was_created=&nscore=&perpage=20&lop=and&find=&find=Start+Search', '2026-07-20 14:30:00+00');

-- 3. records
INSERT INTO records (id, doi, title, description, source_url) VALUES
  (1, '10.1001/arxiv.2101.00001', 'A Survey of Machine Learning', 'Comprehensive review of machine learning techniques.', 'https://arxiv.org/abs/2101.00001'),
  (2, '10.1002/pubmed.123456', 'Genomics in Precision Medicine', 'Discussion of genomic methods for personalized treatments.', 'https://pubmed.ncbi.nlm.nih.gov/123456'),
  (3, '10.1109/5.771073', 'Autonomous Robotics Systems', 'Overview of robotics systems with autonomous navigation.', 'https://ieeexplore.ieee.org/document/771073'),
  (4, '10.1001/arxiv.2202.00002', 'Advances in Natural Language Processing', 'New transformer architectures and benchmarks.', 'https://arxiv.org/abs/2202.00002');

-- 4. users
INSERT INTO users (id, name, username, email, is_marked_for_deletion, deletion_requested_at) VALUES
  (1, 'Alice Johnson', 'alicej', 'alice@example.com', FALSE, NULL),
  (2, 'Bob Martinez',  'bobm',   'bob@example.com',   FALSE, NULL);
  
-- 5. user_search_queries
INSERT INTO user_search_queries (id, user_id, search_query_id, created_at) VALUES
  (1, 1, 1, '2026-07-01 10:00:00+00'),
  (2, 1, 2, '2026-07-05 11:30:00+00'),
  (3, 2, 3, '2026-07-10 14:15:00+00');

-- 6. source_records
INSERT INTO source_records (id, record_id, source_id) VALUES
  (1, 1, 1),
  (2, 4, 1),
  (3, 2, 2),
  (4, 3, 3);

-- 7. search_query_records (Composite PK)
-- Intentionally empty: search_queries 1-3 are all PEDro (source_id 4), but none of the
-- seed records above are PEDro-sourced (they're arXiv/PubMed/IEEE), so there's no record
-- here that could legitimately link to them - a real scrape always links a search query
-- only to records from its own source. Earlier versions of this file linked them anyway,
-- which silently inflated those queries' record counts with records from unrelated
-- sources. Real search_query_records rows for these queries come from the background
-- poller actually scraping PEDro once the app is running.

-- Reset identity sequence generators to avoid primary key conflicts on future INSERTs
SELECT setval(pg_get_serial_sequence('sources', 'id'), (SELECT MAX(id) FROM sources));
SELECT setval(pg_get_serial_sequence('search_queries', 'id'), (SELECT MAX(id) FROM search_queries));
SELECT setval(pg_get_serial_sequence('records', 'id'), (SELECT MAX(id) FROM records));
SELECT setval(pg_get_serial_sequence('users', 'id'), (SELECT MAX(id) FROM users));
SELECT setval(pg_get_serial_sequence('user_search_queries', 'id'), (SELECT MAX(id) FROM user_search_queries));
SELECT setval(pg_get_serial_sequence('source_records', 'id'), (SELECT MAX(id) FROM source_records));