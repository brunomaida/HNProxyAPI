# Hacker News Proxy API

This solution is a RESTFUL API built with **.NET 8 (C#)** that acts as an optimized, intelligent proxy for Hacker News.

The project was architected following **High Performance** and **Zero-Allocation** principles, prioritizing low latency, high concurrency, efficient memory usage, and real-time observability.

It allows custom configuration for network and memory parameters used to limit/expand it's multitheread capabilites and internal memory resources.
The goal is to serve sorted data instantly while minimizing network traffic and Garbage Collector (GC) stress.

This first version uses a single optimized instance for faster release and avoid multiple integration tests with distribuited
architecture and other features related to large scaled systems. The goal, as requested, was to build an intelligent architecture which:
- Warms up collected initial data to hold it on memory for faster query replys and to avoid unecessary repeatable query to hacker news
- Detects differences among new and old IDs compared to the ones stored in memory:
  - New IDs only are retrieved from the Details URL
  - Old IDs are discarded from memory
  - List is always ordered using a descending score value

## 🏃 How to Run the Application

You can execute the application using the .NET CLI, Docker, or Visual Studio. Before starting, ensure you have the **.NET 8.0 SDK** installed.
*(please also read attention.swashbuckle.md for package version compatility)**

### Step 0: Environment Setup
1.  **Configuration:** Review `src/HNProxyAPI.Api/appsettings.json` to ensure default parameters (e.g., `MaxConcurrentRequests`) suit your environment.
2.  **HTTPS Certificates:** If running .NET 8 locally for the first time, trust the development certificate to avoid SSL errors:
    ```bash
    dotnet dev-certs https --trust
    ```

### Option A: Using .NET CLI (Recommended for Devs)
1.  Navigate to the **API project** directory:
    ```bash
    cd HNProxyAPI

2.  Restore dependencies and build the project:
    ```bash
    dotnet restore
    dotnet build
    ```
3.  Run the application:
    ```bash
    dotnet run
    ```
    > **Tip:** Use `dotnet watch run` to enable Hot Reload during development.

4.  **Verify:** The console will display the listening ports. Open your browser to:
    **Swagger UI:** `https://localhost:7100/swagger` (Check console for the exact port)

### Option B: Using Docker (Production Simulation)
Run the API in an isolated Linux container to simulate a production environment.

1.  Navigate to the **solution root** folder.
2.  Build the Docker image:
    ```bash
    docker build -t hnapi-proxy -f HNProxyAPI/Dockerfile .
    ```
3.  Run the container (mapping port 8080):
    ```bash
    docker run -d -p 8080:8080 --name hnapi-instance hnapi-proxy
    ```
4.  **Verify:** Access the API via `http://localhost:8080/swagger`.

### Option C: Using Visual Studio 2022
1.  Open `HNProxyAPI.sln`.
2.  In the Solution Explorer, right-click the **HNProxyAPI** project.
3.  Select **"Set as Startup Project"**.
4.  Press **F5** (Debug) or **Ctrl+F5** (Run without Debugging).
5.  The browser will launch automatically pointing to the Swagger UI 

--- 

## 🏗 Architectural Decisions and Design

### 1. The Importance of the Initial Query (ID Snapshot)
The architecture utilizes a **"Two-Phase Fetch"** pattern. The first call to the `beststories.json` endpoint returns only a lightweight list of Integers (IDs).
* **Why:** This list acts as the **Source of Truth**. It allows the system to calculate the **Delta** (the difference between what exists on the remote server and what is already in local memory). This prevents the blind download of megabytes of unnecessary JSON data.

### 2. Delta Ingestion (New Objects Only)
The system checks for the existence of each ID in the local Cache (`ConcurrentDictionary`) before triggering an HTTP request for details.
* **Why:** We implement **Ingress Deduplication**. If ID `12345` is already in memory, it is skipped. This saves bandwidth, drastically reduces CPU time spent on deserialization, and lowers pressure on the Managed Heap, ensuring that only fresh data consumes resources.

### 3. Hybrid Observability (Network & Memory)
We leverage `System.Diagnostics.Metrics` to instrument two distinct and critical layers:
* **Network Metrics:** Measure latency, throughput, and external API errors. These are used to identify whether slowness is due to the provider (Hacker News) or our internal infrastructure.
* **Memory Metrics:** Measure allocated bytes, item counts, and average object sizes. These are used to prevent **Out Of Memory (OOM)** errors.

### 4. Dynamic Configuration & Scalability (Hot Reload)
Critical parameters such as `MaxConcurrentRequests` and `MaxMemoryThresholdBytes` are injected via `IOptionsMonitor`.
* **Why:** This allows adjusting the "flow rate" at runtime. If the server is under stress, concurrency can be reduced without restarting the application (**Zero Downtime**). Conversely, if resources are ample, processing capacity can be increased instantly.

### 5. Modularity with Extension Methods
`Program.cs` is kept clean using extension methods (e.g., `AddHackerNewsArchitecture`).
* **Why:** Promotes the **Single Responsibility Principle (SRP)**. Dependency injection configuration is isolated, making the code modular. This facilitates readability for new developers and simplifies integration testing, as entire modules can be easily swapped for Mocks.

### 6. Use of `record struct` for incoming/outgoing data
Data structures (e.g., `Story`) are defined as `readonly record struct` with custom serializers.
* **Stack Allocation:** Small structs tend to be allocated on the Stack (or inlined in Arrays), avoiding Managed Heap fragmentation.
* **Immutability:** Ensures natural **Thread Safety** in high-concurrency environments.
* **Performance:** Reduces pressure on the Garbage Collector (Gen0/Gen1), which is vital for maintaining stable API latency (avoiding GC Pauses).

---

