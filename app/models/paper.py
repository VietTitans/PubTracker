from sqlalchemy import Column, Integer, String, Text, DateTime
from sqlalchemy.orm import relationship
from .base import Base
from datetime import datetime

class Paper(Base):
    __tablename__ = "papers"

    id = Column(Integer, primary_key=True)
    doi = Column(String(255), nullable=False, unique=True)
    title = Column(Text, nullable=False)
    abstract = Column(Text)
    url = Column(Text, nullable=False)
    date_first_discovered = Column(DateTime, nullable=False, default=datetime.utcnow)

    records = relationship("Record", back_populates="paper", cascade="all, delete-orphan")
