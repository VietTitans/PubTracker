INSERT INTO sources (name, base_url) VALUES
  ('arXiv', 'https://arxiv.org'),
  ('PubMed', 'https://pubmed.ncbi.nlm.nih.gov'),
  ('IEEE Xplore', 'https://ieeexplore.ieee.org');

INSERT INTO search_queries (source_id, target_url, last_digest_sent_at) VALUES
  (1, 'https://arxiv.org/search/?query=machine+learning&searchtype=all', '2026-07-15 09:00:00+00'),
  (1, 'https://arxiv.org/search/?query=natural+language+processing&searchtype=all', '2026-07-20 14:30:00+00'),
  (2, 'https://pubmed.ncbi.nlm.nih.gov/?term=genomics', NULL),
  (3, 'https://ieeexplore.ieee.org/search/searchresult.jsp?queryText=robotics', '2026-07-18 18:45:00+00');

INSERT INTO records (doi, title, description) VALUES
  ('10.1001/arxiv.2101.00001', 'A Survey of Machine Learning', 'Comprehensive review of machine learning techniques.'),
  ('10.1002/pubmed.123456', 'Genomics in Precision Medicine', 'Discussion of genomic methods for personalized treatments.'),
  ('10.1109/5.771073', 'Autonomous Robotics Systems', 'Overview of robotics systems with autonomous navigation.'),
  ('10.1001/arxiv.2202.00002', 'Advances in Natural Language Processing', 'New transformer architectures and benchmarks.');

INSERT INTO users (name, username, email) VALUES
  ('Alice Johnson', 'alicej', 'alice@example.com'),
  ('Bob Martinez', 'bobm', 'bob@example.com');

INSERT INTO user_search_queries (user_id, search_query_id) VALUES
  (1, 1),
  (1, 2),
  (2, 3),
  (2, 4);

INSERT INTO source_records (record_id, source_id) VALUES
  (1, 1),
  (4, 1),
  (2, 2),
  (3, 3);

INSERT INTO search_query_records (search_query_id, record_id, first_seen_at) VALUES
  (1, 1, '2026-07-10 08:15:00+00'),
  (1, 4, '2026-07-20 14:00:00+00'),
  (2, 4, '2026-07-18 12:20:00+00'),
  (3, 2, '2026-07-16 11:05:00+00'),
  (4, 3, '2026-07-17 16:10:00+00'),
  (4, 1, '2026-07-19 09:30:00+00');