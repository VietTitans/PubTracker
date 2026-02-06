from .base import Base
from sqlalchemy import Column, Integer, String
from sqlalchemy.orm import relationship

class Source(Base):
    __tablename__ = "sources"

    id = Column(Integer, primary_key=True)
    name = Column(String(255), nullable=False, unique=True)

    records = relationship("Record", back_populates="source", cascade="all, delete-orphan")
    processing_logs = relationship("SourceProcessingLog", back_populates="source", cascade="all, delete-orphan")
