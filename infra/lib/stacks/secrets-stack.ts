import { CfnOutput, Stack } from 'aws-cdk-lib';
import * as secretsmanager from 'aws-cdk-lib/aws-secretsmanager';
import { Construct } from 'constructs';
import type { SecretsOutputs, SharedStackProps } from './stack-props';

export class SecretsStack extends Stack {
  public readonly outputs: SecretsOutputs;

  public constructor(scope: Construct, id: string, props: SharedStackProps) {
    super(scope, id, props);

    const openAiSecret = new secretsmanager.Secret(this, 'OpenAiApiKey', {
      secretName: `thc-meal-planner/${props.deploymentConfig.name}/openai-api-key`
    });

    const appSecrets = new secretsmanager.Secret(this, 'AppSecrets', {
      secretName: `thc-meal-planner/${props.deploymentConfig.name}/app-secrets`
    });

    this.outputs = {
      openAiSecretArn: openAiSecret.secretArn,
      appSecretsArn: appSecrets.secretArn
    };

    new CfnOutput(this, 'OpenAiSecretArn', { value: this.outputs.openAiSecretArn });
    new CfnOutput(this, 'AppSecretsArn', { value: this.outputs.appSecretsArn });
  }
}
