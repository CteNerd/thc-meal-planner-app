import { CfnOutput, Stack } from 'aws-cdk-lib';
import * as s3 from 'aws-cdk-lib/aws-s3';
import { Construct } from 'constructs';
import { DynamoTable } from '../constructs/dynamo-table';
import type { DataOutputs, SharedStackProps } from './stack-props';

export class DataStack extends Stack {
  public readonly outputs: DataOutputs;

  public constructor(scope: Construct, id: string, props: SharedStackProps) {
    super(scope, id, props);

    const prefix = `thc-meal-planner-${props.deploymentConfig.name}`;
    const tableConfigs = [
      { key: 'Users', ttl: undefined },
      { key: 'MealPlans', ttl: 'TTL' },
      { key: 'Recipes', ttl: undefined },
      { key: 'Favorites', ttl: undefined },
      { key: 'GroceryLists', ttl: undefined },
      { key: 'ChatHistory', ttl: 'TTL' }
    ];

    const tableNames = Object.fromEntries(
      tableConfigs.map(({ key, ttl }) => {
        const table = new DynamoTable(this, `${key}Table`, {
          tableName: `${prefix}-${key.toLowerCase()}`,
          ttlAttribute: ttl
        });

        return [key, table.table.tableName];
      })
    );

    const recipeImagesBucket = new s3.Bucket(this, 'RecipeImagesBucket', {
      bucketName: `${prefix}-recipe-images`,
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      enforceSSL: true
    });

    this.outputs = {
      tableNames,
      recipeImagesBucketName: recipeImagesBucket.bucketName
    };

    new CfnOutput(this, 'RecipeImagesBucketName', { value: this.outputs.recipeImagesBucketName });
  }
}
