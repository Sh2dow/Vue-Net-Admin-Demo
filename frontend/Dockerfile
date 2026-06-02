FROM node:22-alpine AS build
WORKDIR /app

ARG VITE_AUTHORITY
ARG VITE_API_URL
ENV VITE_AUTHORITY=${VITE_AUTHORITY}
ENV VITE_API_URL=${VITE_API_URL}

COPY frontend/package.json frontend/bun.lock ./
RUN npm install

COPY frontend/ ./
RUN npm run build

FROM nginx:1.27-alpine AS final
WORKDIR /usr/share/nginx/html

COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist ./

EXPOSE 80
