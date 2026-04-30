from sqlalchemy import Column, Integer, String, ForeignKey, DateTime, UniqueConstraint
from sqlalchemy.orm import relationship
from .base import Base
from datetime import datetime

class Record(Base):
    __tablename__ = "records"
    __table_args__ = (
        UniqueConstraint("source_id", "external_id"),
        UniqueConstraint("paper_id", "source_id"),
    )

    id = Column(Integer, primary_key=True)
    paper_id = Column(Integer, ForeignKey("papers.id", ondelete="CASCADE"), nullable=False)
    source_id = Column(Integer, ForeignKey("sources.id", ondelete="CASCADE"), nullable=False)
    external_id = Column(String(255), nullable=False)
    date_discovered = Column(DateTime, nullable=False, default=datetime.utcnow)

    paper = relationship("Paper", back_populates="records")
    source = relationship("Source", back_populates="records")