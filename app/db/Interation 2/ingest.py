from repositories.sources_repo import SourcesRepo
from repositories.papers_repo import PapersRepo
from repositories.records_repo import RecordsRepo

def ingest_record(source_name, record):
    """
    record is a PaperRecord dataclass from a connector
    """

    source_id = SourcesRepo.get_or_create(source_name)

    # Check if paper exists by DOI
    paper = PapersRepo.get_by_doi(record.doi)

    if not paper:
        paper_id = PapersRepo.insert(
            doi=record.doi,
            title=record.title,
            abstract=record.abstract,
            url=record.url
        )
    else:
        paper_id = paper.id

    # Insert record linking source + external ID
    record_id = RecordsRepo.insert(
        paper_id=paper_id,
        source_id=source_id,
        external_id=record.external_id
    )

    return record_id
