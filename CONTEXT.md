# PubTracker

Monitors scientific-database searches for new articles and emails subscribers a digest of what is new, with AI-written summaries.

## Language

Terms are canonical for code and docs. Each carries a _UI label_ where user-facing copy uses plainer wording; UI copy follows the label, everything else follows the term.

**Search Query**:
One deduplicated source and search URL, shared by every user who follows it.
_Avoid_: Search, saved search, feed
_UI label_: "Search" (plural "Your searches")

**Subscription**:
One user's link to a Search Query, carrying that user's own digest watermark.
_Avoid_: Follow, user search query
_UI label_: "Subscribe" / "Unsubscribe"

**Record**:
One stored article, shared across every Search Query that returns it.
_Avoid_: Paper, publication, result, item
_UI label_: "Article"

**Baseline poll**:
The first successful poll of a Search Query. It stores the existing backlog and never sends a digest.
_Avoid_: Initial poll, first poll

**Digest**:
The email of Records first seen after a subscriber's watermark, combined across that user's Subscriptions.
_Avoid_: Newsletter, alert, notification
_UI label_: "Digest" (the email)

**Digest summary**:
An AI-written summary of 5-6 sentences covering every new Record found for one Search Query in one poll, based on their abstracts. Not stored on a Record.
_Avoid_: Summary (unqualified), AI summary

**Author intention**:
An AI-written summary of 1-2 sentences, stored once per Record, stating what the authors set out to investigate or show.
_Avoid_: Study aim, description
