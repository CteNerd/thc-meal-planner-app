import type { DeploymentConfig } from './types';

export const devConfig: DeploymentConfig = {
  name: 'dev',
  account: process.env.CDK_DEFAULT_ACCOUNT,
  region: 'us-east-1',
  domainName: undefined,
  lambda: {
    memory: 512,
    timeout: 30
  },
  apiGateway: {
    throttle: {
      rateLimit: 50,
      burstLimit: 100
    }
  }
};
