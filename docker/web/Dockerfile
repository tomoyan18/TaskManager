# syntax=docker/dockerfile:1
# TaskManager Web（SPA の配信と /api の中継）。docs/design/architecture.md §7

FROM node:24-alpine3.24 AS build
WORKDIR /app
COPY frontend/task-manager-web/package.json frontend/task-manager-web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/task-manager-web/ ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.30.5-alpine AS runtime
COPY docker/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
