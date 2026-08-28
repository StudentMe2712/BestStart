"""FastAPI entrypoint and application setup for TrendScanner."""

import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api.routers import router as api_router
from app.core.settings import settings
from app.db.database import init_db
from app.workers.scheduler import shutdown_scheduler, start_scheduler

logging.basicConfig(
    level=logging.DEBUG if settings.DEBUG else logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger("TrendScanner")


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan context manager for startup and shutdown hooks."""
    logger.info("Initializing SQLite database...")
    init_db(seed_default_sources=True)

    logger.info("Starting background scheduler...")
    start_scheduler()

    yield

    logger.info("Shutting down background scheduler...")
    shutdown_scheduler()


app = FastAPI(
    title=settings.PROJECT_NAME,
    description="Autonomous local analytical terminal for business trends and micro-niches",
    version="0.1.0",
    lifespan=lifespan,
    docs_url="/docs",
    redoc_url="/redoc",
)

# Setup CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Mount API Routers
app.include_router(api_router)


@app.get("/", tags=["Health"])
async def root():
    """Root health check endpoint."""
    return {
        "service": settings.PROJECT_NAME,
        "status": "online",
        "version": "0.1.0",
        "docs": "/docs",
    }


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "main:app",
        host=settings.APP_HOST,
        port=settings.APP_PORT,
        reload=settings.DEBUG,
    )
