# Stage 1: Build
FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . .
RUN npm run build

# Stage 2: Serve using Nginx
FROM nginx:alpine
# نسخ ملفات الرياكت المبنية إلى مجلد الـ Nginx الديفولت
COPY --from=build /app/dist /usr/share/nginx/html

# نسخ إعدادات النغينكس لتشغيل البروكسي والـ WebSockets بدون وجع راس الـ CORS
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 5173
CMD ["nginx", "-g", "daemon off;"]