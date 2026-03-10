# 🚚 Vantage Fleet - Fleet Tracker

A real-time, event-driven fleet tracking dashboard built with a microservices architecture. This system simulates GPS telemetry from a fleet of vehicles navigating the streets of Ahmedabad, streams the data through Apache Kafka, and visualizes it in real-time on a dark-themed Leaflet map.

![Angular](https://img.shields.io/badge/Angular-DD0031?style=for-the-badge&logo=angular&logoColor=white)
![.NET Core](https://img.shields.io/badge/.NET_7-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Apache Kafka](https://img.shields.io/badge/Apache_Kafka-231F20?style=for-the-badge&logo=apachekafka&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-DC382D?style=for-the-badge&logo=redis&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-336791?style=for-the-badge&logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)

## 🏗️ System Architecture

This project utilizes a CQRS (Command Query Responsibility Segregation) pattern, separating the high-throughput ingestion of telemetry data from the real-time broadcasting and historical querying.

1. **Fleet Simulator (C# Console):** Dynamically generates distinct routes using OSRM and posts simulated GPS coordinates.
2. **Ingestion API (.NET 7):** The gateway that validates and publishes incoming coordinates to a Kafka topic.
3. **Apache Kafka:** The core message broker ensuring reliable, high-throughput event streaming.
4. **State Service (.NET 7):** Consumes the Kafka stream, caches the latest truck coordinates in Redis, and broadcasts live updates to the frontend via SignalR WebSockets.
5. **Historical Service (.NET 7):** Consumes the Kafka stream in batches, translating raw coordinates into PostGIS spatial geometry, and saving trip histories into PostgreSQL. Utilizes API Output Caching to protect the database from heavy read queries.
6. **Frontend (Angular 16+):** A dark-themed dashboard using Leaflet.js to draw live truck markers and historical route polylines.

## 📂 Repository Structure

```text
fleet-tracker-workspace/
├── backend/
│   ├── HistoricalService/   # Postgres/PostGIS batch writer & REST API
│   ├── IngestionService/    # Kafka Producer API
│   └── StateService/        # Redis Cache & SignalR WebSocket Hub
├── frontend/                # Angular SPA with Leaflet maps
├── simulator/
│   └── FleetSimulator/      # C# Route Generator
├── docker-compose.yml       # Master container orchestration
├── .env.example             # Template for environment secrets
└── README.md
```

## 🚀 Getting Started

### Prerequisites
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- [.NET 7.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) (For local development)
- [Node.js](https://nodejs.org/) v18+ & Angular CLI (For local frontend development)

### 1. Configuration
Clone the repository and set up your environment variables:
```bash
git clone https://github.com/yourusername/fleet-tracker.git
cd fleet-tracker-workspace

# Copy the example environment file
cp .env.example .env
```
*(Note: You can adjust the default passwords in `.env` if desired.)*

### 2. Running via Docker (Recommended)
You can spin up the entire infrastructure (Zookeeper, Kafka, Redis, Postgres/PostGIS) and the microservices using Docker Compose.

```bash
docker-compose up -d --build
```
This will start:
- 🐘 **Postgres** on port `5433`
- 🧠 **Redis** on port `6379`
- 📨 **Kafka** on port `9092`
- 🔌 **Ingestion API** on port `5195`
- 📡 **State API** on port `5196`
- 📜 **Historical API** on port `5200`
- 🌐 **Frontend UI** on port `4200`

Access the dashboard at [http://localhost:4200](http://localhost:4200).

### 3. Running the Simulator
To actually see vehicles moving on the map, you need to start the simulator. The simulator posts GPS coordinates to the Ingestion API.

Open a new terminal and run:
```bash
cd simulator/FleetSimulator
dotnet run
```
You should now see trucks driving across the map in your browser!

### 4. Local Development (Without Dockerizing Apps)
If you prefer to run the C# APIs and Angular app locally via your IDE (while keeping the databases in Docker):

1. Start only the infrastructure:
   ```bash
   docker-compose up -d zookeeper kafka redis postgres
   ```
2. Start the Backend APIs:
   ```bash
   dotnet run --project backend/IngestionService
   dotnet run --project backend/StateService
   dotnet run --project backend/HistoricalService
   ```
3. Start the Frontend:
   ```bash
   cd frontend
   npm install
   npm start
   ```