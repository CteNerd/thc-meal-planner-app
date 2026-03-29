export type DeploymentConfig = {
  name: 'dev' | 'prod';
  account?: string;
  region: string;
  domainName?: string;
  hostedZoneId?: string;
  hostedZoneName?: string;
  lambda: {
    memory: number;
    timeout: number;
  };
  apiGateway: {
    throttle: {
      rateLimit: number;
      burstLimit: number;
    };
  };
  budgets: {
    monthlyLimitUsd: number;
    alertThresholdPercent: number;
    alertEmails: string[];
  };
};
