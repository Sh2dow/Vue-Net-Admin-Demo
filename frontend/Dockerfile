FROM node:22-alpine AS build
WORKDIR /app

COPY package.json ./
RUN npm install --legacy-peer-deps

COPY . ./
RUN npm run build

FROM nginx:1.27-alpine AS final
WORKDIR /usr/share/nginx/html

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY config.json /usr/share/nginx/html/config.json
COPY --from=build /app/dist ./

EXPOSE 80
