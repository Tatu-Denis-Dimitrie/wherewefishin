export const environment = {
  production: true,
  // Empty string = relative URLs; nginx inside Docker proxies /api/ → backend:8080
  apiBaseUrl: '',
  // Empty string = nginx proxies /outputs/ → fish-recognition:5001
  pythonServiceUrl: '',
};
