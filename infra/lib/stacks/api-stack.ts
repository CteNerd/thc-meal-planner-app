import { CfnOutput, Stack } from 'aws-cdk-lib';
import * as apigateway from 'aws-cdk-lib/aws-apigateway';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import * as iam from 'aws-cdk-lib/aws-iam';
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
        RECIPE_IMAGES_BUCKET: props.data.recipeImagesBucketName,
        DynamoDb__PartitionKeyName: 'PK',
        DynamoDb__SortKeyName: 'SK',
        DynamoDb__Tables__UserProfileDocument: props.data.tableNames.Users,
        DynamoDb__Tables__DependentProfileDocument: props.data.tableNames.Users,
        DynamoDb__Tables__RecipeDocument: props.data.tableNames.Recipes,
        DynamoDb__Tables__FavoriteRecipeDocument: props.data.tableNames.Favorites,
        DynamoDb__Tables__MealPlanDocument: props.data.tableNames.MealPlans,
        DynamoDb__Tables__GroceryListDocument: props.data.tableNames.GroceryLists,
        DynamoDb__Tables__PantryStaplesDocument: props.data.tableNames.GroceryLists,
        DynamoDb__Tables__ChatHistoryMessageDocument: props.data.tableNames.ChatHistory
      }
    });

    const recipeImagesBucket = s3.Bucket.fromBucketName(this, 'RecipeImagesBucket', props.data.recipeImagesBucketName);
    recipeImagesBucket.grantReadWrite(handler.function);

    Object.values(props.data.tableNames).forEach((tableName) => {
      const table = dynamodb.Table.fromTableName(this, `${tableName}Ref`, tableName);
      table.grantReadWriteData(handler.function);

      handler.function.addToRolePolicy(new iam.PolicyStatement({
        actions: ['dynamodb:Query', 'dynamodb:Scan'],
        resources: [`${table.tableArn}/index/*`]
      }));
    });

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
