import { CfnOutput, Stack } from 'aws-cdk-lib';
import * as apigateway from 'aws-cdk-lib/aws-apigateway';
import * as s3 from 'aws-cdk-lib/aws-s3';
import { Construct } from 'constructs';
import { DotnetLambdaFunction } from '../constructs/lambda-function';
import type { ApiOutputs, ApiStackProps } from './stack-props';

export class ApiStack extends Stack {
  public readonly outputs: ApiOutputs;

  public constructor(scope: Construct, id: string, props: ApiStackProps) {
    super(scope, id, props);

    const prefix = `thc-meal-planner-${props.deploymentConfig.name}`;
    const handler = new DotnetLambdaFunction(this, 'ApiHandler', {
      functionName: `${prefix}-api-handler`,
      memorySize: props.deploymentConfig.lambda.memory,
      timeoutSeconds: props.deploymentConfig.lambda.timeout,
      environment: {
        ASPNETCORE_ENVIRONMENT: props.deploymentConfig.name === 'prod' ? 'Production' : 'Development',
        COGNITO_USER_POOL_ID: props.auth.userPoolId,
        COGNITO_CLIENT_ID: props.auth.userPoolClientId,
        OPENAI_SECRET_ARN: props.secrets.openAiSecretArn,
        TABLE_PREFIX: prefix,
        RECIPE_IMAGES_BUCKET: props.data.recipeImagesBucketName
      }
    });

    const recipeImagesBucket = s3.Bucket.fromBucketName(this, 'RecipeImagesBucket', props.data.recipeImagesBucketName);
    recipeImagesBucket.grantReadWrite(handler.function);

    const api = new apigateway.LambdaRestApi(this, 'RestApi', {
      restApiName: `${prefix}-api`,
      handler: handler.function,
      proxy: true,
      deployOptions: {
        stageName: props.deploymentConfig.name,
        throttlingBurstLimit: props.deploymentConfig.apiGateway.throttle.burstLimit,
        throttlingRateLimit: props.deploymentConfig.apiGateway.throttle.rateLimit
      },
      defaultCorsPreflightOptions: {
        allowOrigins: apigateway.Cors.ALL_ORIGINS,
        allowMethods: apigateway.Cors.ALL_METHODS,
        allowHeaders: ['*']
      }
    });

    this.outputs = {
      apiUrl: api.url
    };

    new CfnOutput(this, 'ApiUrl', { value: this.outputs.apiUrl });
  }
}
