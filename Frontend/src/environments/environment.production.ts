export const environment = {
  production: true,
  // Empty string = relative URLs; nginx inside Docker proxies /api/ → backend:8080
  apiBaseUrl: '',
  // Empty string = nginx proxies /outputs/ → fish-recognition:5001
  pythonServiceUrl: '',
  stripePublishableKey: 'pk_test_51T3tmZLbUF11stB2s4FUN6xMWGAhgT9P9daOS7hFjpB5X4VZldHe5pScprP7D1hJPjL1sM7JVOzsUBhX6rVyqEVt00Ucyq4JYv',
};
