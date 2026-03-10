# Build Stage: Compile the Angular TypeScript into optimized static HTML/JS
FROM node:18 AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build --configuration=production

# Serve Stage: Host the static files using a lightweight Nginx web server
FROM nginx:alpine
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
EXPOSE 80