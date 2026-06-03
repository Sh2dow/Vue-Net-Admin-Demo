FROM node:22-alpine AS build
WORKDIR /app

ARG VITE_AUTHORITY
ARG VITE_API_URL

COPY frontend/package.json frontend/bun.lock ./
RUN npm install --legacy-peer-deps

COPY frontend/ ./
RUN printf "VITE_AUTHORITY=%s\nVITE_API_URL=%s\n" "$VITE_AUTHORITY" "$VITE_API_URL" > .env.production && npm run build

FROM nginx:1.27-alpine AS final
WORKDIR /usr/share/nginx/html

COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist ./

EXPOSE 80
