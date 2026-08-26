# Stage 1: Build
FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . .
ARG VITE_INGESTION_API_KEY=dev-ingestion-key
ARG VITE_DASHBOARD_API_KEY=dev-dashboard-key
ENV VITE_INGESTION_API_KEY=$VITE_INGESTION_API_KEY
ENV VITE_DASHBOARD_API_KEY=$VITE_DASHBOARD_API_KEY
RUN npm run build

# Stage 2: Serve using Nginx
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html

COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 5173
CMD ["nginx", "-g", "daemon off;"]