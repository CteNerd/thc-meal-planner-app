#!/usr/bin/env node
import 'source-map-support/register';
import * as cdk from 'aws-cdk-lib';
import { devConfig } from '../lib/config/dev';
import { prodConfig } from '../lib/config/prod';
import type { DeploymentConfig } from '../lib/config/types';
import { AuthStack } from '../lib/stacks/auth-stack';
import { DataStack } from '../lib/stacks/data-stack';
import { ApiStack } from '../lib/stacks/api-stack';
import { FrontendStack } from '../lib/stacks/frontend-stack';
import { NotificationsStack } from '../lib/stacks/notifications-stack';
import { SecretsStack } from '../lib/stacks/secrets-stack';

const app = new cdk.App();

const envName = app.node.tryGetContext('env') ?? 'dev';
const config: DeploymentConfig = envName === 'prod' ? prodConfig : devConfig;

const env: cdk.Environment = {
  account: config.account ?? process.env.CDK_DEFAULT_ACCOUNT,
  region: config.region ?? process.env.CDK_DEFAULT_REGION
};

const stackPrefix = `ThcMealPlanner-${config.name}`;

const authStack = new AuthStack(app, `${stackPrefix}-Auth`, {
  env,
  deploymentConfig: config
});

const dataStack = new DataStack(app, `${stackPrefix}-Data`, {
  env,
  deploymentConfig: config
});

const secretsStack = new SecretsStack(app, `${stackPrefix}-Secrets`, {
  env,
  deploymentConfig: config
});

const apiStack = new ApiStack(app, `${stackPrefix}-Api`, {
  env,
  deploymentConfig: config,
  auth: authStack.outputs,
  data: dataStack.outputs,
  secrets: secretsStack.outputs
});
apiStack.addDependency(authStack);
apiStack.addDependency(dataStack);
apiStack.addDependency(secretsStack);

const frontendStack = new FrontendStack(app, `${stackPrefix}-Frontend`, {
  env,
  deploymentConfig: config,
  api: apiStack.outputs,
  data: dataStack.outputs
});
frontendStack.addDependency(apiStack);
frontendStack.addDependency(dataStack);

new NotificationsStack(app, `${stackPrefix}-Notifications`, {
  env,
  deploymentConfig: config
});
