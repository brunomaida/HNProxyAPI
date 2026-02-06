## 🔮 Roadmap and Future

To evolve this solution while maintaining the current architecture, the following features are planned:
1.  **Distributed Cache:** Replace or complement the in-memory cache with Redis, allowing multiple API instances to share the same state (**Horizontal Scaling**).
2.  **Advanced Resilience:** Implement a **Circuit Breaker** to temporarily stop requests to the external API if the error rate exceeds a safe threshold.
3.  **Real-time Push:** Implement WebSockets to notify the front-end client as soon as data reordering is complete, eliminating the need for client-side polling.
4.  **Response Compression:** Add compression mechanisms to reduce the JSON payload size sent to the end client if query size or data structure increases.
5.  **Authentication:** Protect the consumption endpoint to control quotas (**Rate Limiting**) per specific client.