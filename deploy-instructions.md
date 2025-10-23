# Snapx API - Kubernetes Deployment Guide

## Tổng quan
Snapx API là một .NET 8 Web API sử dụng yt-dlp và ffmpeg để download và xử lý video từ các nền tảng như TikTok, YouTube, v.v.

## Cấu trúc Project
- **Snapx.Api**: Main API project (.NET 8)
- **Snapx.Application**: Application layer với business logic
- **Snapx.Domain**: Domain entities và DTOs
- **Snapx.Infrastructure**: External services (yt-dlp, ffmpeg)

## Files được tạo
1. `Dockerfile` - Multi-stage build cho .NET 8 API
2. `k8s-deployment.yaml` - Kubernetes Deployment, Service, ConfigMap
3. `k8s-service.yaml` - Service với LoadBalancer và Ingress
4. `docker-compose.yml` - Local development setup

## Hướng dẫn Deploy

### 1. Build Docker Image
```bash
# Build image
docker build -t snapx-api:latest .

# Tag cho registry (thay your-registry bằng registry của bạn)
docker tag snapx-api:latest your-registry/snapx-api:latest

# Push to registry
docker push your-registry/snapx-api:latest
```

### 2. Deploy lên Kubernetes

#### Option 1: Sử dụng file deployment tổng hợp
```bash
kubectl apply -f k8s-deployment.yaml
```

#### Option 2: Deploy từng component riêng biệt
```bash
# Deploy service trước
kubectl apply -f k8s-service.yaml

# Deploy deployment
kubectl apply -f k8s-deployment.yaml
```

### 3. Kiểm tra deployment
```bash
# Kiểm tra pods
kubectl get pods -l app=snapx-api

# Kiểm tra services
kubectl get services

# Kiểm tra logs
kubectl logs -l app=snapx-api

# Kiểm tra ingress
kubectl get ingress
```

### 4. Test API
```bash
# Port forward để test local
kubectl port-forward service/snapx-api-service 8080:80

# Test API
curl http://localhost:8080/api/MediaDownload/media-download \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{"url": "https://example.com/video"}'
```

## Cấu hình Production

### 1. Resource Limits
- **Memory**: 512Mi request, 2Gi limit
- **CPU**: 250m request, 1000m limit
- **Replicas**: 3 instances

### 2. Health Checks
- **Liveness Probe**: `/health` endpoint, 30s initial delay
- **Readiness Probe**: `/health` endpoint, 5s initial delay

### 3. Storage
- **TempStorage**: EmptyDir volume cho temporary files
- **Persistent Volume**: Có thể cần cho production data

### 4. Environment Variables
- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://+:80`

## Monitoring và Logging

### 1. Health Check Endpoint
API cần implement health check endpoint:
```csharp
[HttpGet("health")]
public IActionResult Health()
{
    return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}
```

### 2. Logging
- Structured logging với Serilog
- Log levels: Information cho general, Warning cho Microsoft.AspNetCore

## Troubleshooting

### 1. Pod không start
```bash
kubectl describe pod <pod-name>
kubectl logs <pod-name>
```

### 2. Service không accessible
```bash
kubectl get endpoints
kubectl describe service snapx-api-service
```

### 3. Image pull issues
```bash
kubectl describe pod <pod-name> | grep -i image
```

## Security Considerations

1. **Network Policies**: Implement network policies để restrict traffic
2. **RBAC**: Sử dụng ServiceAccount với minimal permissions
3. **Secrets**: Store sensitive data trong Kubernetes Secrets
4. **Image Security**: Scan Docker images cho vulnerabilities

## Scaling

### Horizontal Pod Autoscaler (HPA)
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: snapx-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: snapx-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

## Cleanup
```bash
# Xóa tất cả resources
kubectl delete -f k8s-deployment.yaml
kubectl delete -f k8s-service.yaml
```
