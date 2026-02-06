from sqlalchemy import Column, Integer, String, Boolean, DateTime, ForeignKey, JSON, Table
from sqlalchemy.orm import relationship
from .base import Base
from datetime import datetime

# Many-to-many association table for subscriptions
subscriber_saved_search_subscriptions = Table(
    "subscriber_saved_search_subscriptions",
    Base.metadata,
    Column("subscriber_id", Integer, ForeignKey("subscribers.id", ondelete="CASCADE"), primary_key=True),
    Column("saved_search_id", Integer, ForeignKey("saved_searches.id", ondelete="CASCADE"), primary_key=True),
    # unique constraint enforced by primary keys
)

class SavedSearch(Base):
    __tablename__ = "saved_searches"

    id = Column(Integer, primary_key=True)
    subscriber_id = Column(Integer, ForeignKey("subscribers.id", ondelete="SET NULL"), nullable=True)  # owner
    name = Column(String(255), nullable=False)
    query_params = Column(JSON, nullable=False)
    is_active = Column(Boolean, nullable=False, default=True)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)
    updated_at = Column(DateTime, nullable=False, default=datetime.utcnow)

    subscriber = relationship("Subscriber", back_populates="saved_searches")
    notifications = relationship("Notification", back_populates="search", cascade="all, delete-orphan")

    # subscribers following this search (including owner optionally)
    followers = relationship(
        "Subscriber",
        secondary=subscriber_saved_search_subscriptions,
        back_populates="subscribed_searches"
    )
