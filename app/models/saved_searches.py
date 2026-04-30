from sqlalchemy import Column, Integer, String, Boolean, DateTime, ForeignKey, JSON, Table, Text
from sqlalchemy.orm import relationship
from .base import Base
from datetime import datetime

class SavedSearch(Base):
    __tablename__ = "saved_searches"

    id = Column(Integer, primary_key=True)
    user_id = Column(Integer, ForeignKey("users.id", ondelete="CASCADE"), nullable=False)

    name = Column(String(255), nullable=False)
    search_query = Column(Text(), nullable=False)
    is_active = Column(Boolean, nullable=False, default=True)

    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow, onupdate=datetime.utcnow)

    user = relationship("User", back_populates="saved_searches")