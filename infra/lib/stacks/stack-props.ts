import type { StackProps } from 'aws-cdk-lib';
import type { DeploymentConfig } from '../config/types';

export type SharedStackProps = StackProps & {
  deploymentConfig: DeploymentConfig;
};

export type AuthOutputs = {
  userPoolId: string;
  userPoolClientId: string;
  userPoolDomain: string;
};

export type DataOutputs = {
  tableNames: Record<string, string>;
  recipeImagesBucketName: string;
};

export type ApiOutputs = {
  apiUrl: string;
};

export type SecretsOutputs = {
  openAiSecretArn: string;
  appSecretsArn: string;
};

export type ApiStackProps = SharedStackProps & {
  auth: AuthOutputs;
  data: DataOutputs;
  secrets: SecretsOutputs;
};

export type FrontendStackProps = SharedStackProps & {
  api: ApiOutputs;
  data: DataOutputs;
};
