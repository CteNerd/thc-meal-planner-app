import type { DeploymentConfig } from './types';

export const prodConfig: DeploymentConfig = {
  name: 'prod',
  account: process.env.CDK_DEFAULT_ACCOUNT,
  region: 'us-east-1',
  domainName: 'meals.example.com',
  lambda: {
    memory: 512,
    timeout: 30
  },
  apiGateway: {
    throttle: {
      rateLimit: 50,
      burstLimit: 100
    }
  },
  budgets: {
    monthlyLimitUsd: 15,
    alertThresholdPercent: 80,
    alertEmails: ['rtomlin62@gmail.com']
  }
};
