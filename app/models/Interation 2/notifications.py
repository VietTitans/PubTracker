from sqlalchemy import Column, Integer, ForeignKey, DateTime, UniqueConstraint
from sqlalchemy.orm import relationship
from .base import Base
from datetime import datetime

class Notification(Base):
    __tablename__ = "notifications"
    __table_args__ = (UniqueConstraint("subscriber_id", "record_id", "search_id"),)

    id = Column(Integer, primary_key=True)
    subscriber_id = Column(Integer, ForeignKey("subscribers.id", ondelete="CASCADE"), nullable=False)
    record_id = Column(Integer, ForeignKey("records.id", ondelete="CASCADE"), nullable=False)
    search_id = Column(Integer, ForeignKey("saved_searches.id", ondelete="CASCADE"), nullable=False)
    sent_at = Column(DateTime, nullable=False, default=datetime.utcnow)

    subscriber = relationship("Subscriber", back_populates="notifications")
    record = relationship("Record", back_populates="notifications")
    search = relationship("SavedSearch", back_populates="notifications")
