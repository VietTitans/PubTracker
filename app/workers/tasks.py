from celery import shared_task
from connectors.pedro import PEDroConnector
from db import (
    get_or_create_source,
    save_paper,
    save_record,
    find_matching_saved_searches,
    create_notifications
)


@shared_task
def run_source_ingestion():
    """
    Runs ingestion for all enabled sources.
    Extend this list as you add more connectors.
    """
    sources = [
        PEDroConnector(search_urls=[
            "https://search.pedro.org.au/advanced-search/results?abstract_with_title=&therapy=...",
            "https://search.pedro.org.au/advanced-search/results?abstract_with_title=&therapy=..."
        ])
    ]

    for connector in sources:
        process_source.delay(connector.source_name)


@shared_task
def process_source(source_name: str):
    """
    Process one connector in its own task (good scaling).
    """
    connector = {
        "PEDro": PEDroConnector([
            "https://search.pedro.org.au/...1",
            "https://search.pedro.org.au/...2"
        ])
    }[source_name]

    source_id = get_or_create_source(source_name)
    records = connector.fetch_records()

    for record in records:
        paper_id = save_paper(record)
        rec_id = save_record(source_id, paper_id, record)

        # match against saved searches
        searches = find_matching_saved_searches(record)
        create_notifications(searches, rec_id)
