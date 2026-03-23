import { CfnOutput, Stack } from 'aws-cdk-lib';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
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
      {
        key: 'Users',
        ttl: undefined,
        globalSecondaryIndexes: [
          {
            indexName: 'FamilyIndex',
            partitionKey: { name: 'familyId', type: dynamodb.AttributeType.STRING },
            sortKey: { name: 'name', type: dynamodb.AttributeType.STRING },
            projectionType: dynamodb.ProjectionType.ALL
          }
        ]
      },
      {
        key: 'MealPlans',
        ttl: 'TTL',
        globalSecondaryIndexes: [
          {
            indexName: 'StatusIndex',
            partitionKey: { name: 'familyId', type: dynamodb.AttributeType.STRING },
            sortKey: { name: 'statusCreatedAt', type: dynamodb.AttributeType.STRING },
            projectionType: dynamodb.ProjectionType.KEYS_ONLY
          }
        ]
      },
      {
        key: 'Recipes',
        ttl: undefined,
        globalSecondaryIndexes: [
          {
            indexName: 'CategoryIndex',
            partitionKey: { name: 'category', type: dynamodb.AttributeType.STRING },
            sortKey: { name: 'name', type: dynamodb.AttributeType.STRING },
            projectionType: dynamodb.ProjectionType.ALL
          },
          {
            indexName: 'CuisineIndex',
            partitionKey: { name: 'cuisine', type: dynamodb.AttributeType.STRING },
            sortKey: { name: 'name', type: dynamodb.AttributeType.STRING },
            projectionType: dynamodb.ProjectionType.ALL
          }
        ]
      },
      { key: 'Favorites', ttl: undefined },
      { key: 'GroceryLists', ttl: undefined },
      { key: 'ChatHistory', ttl: 'TTL' }
    ];

    const tableNames = Object.fromEntries(
      tableConfigs.map(({ key, ttl, globalSecondaryIndexes }) => {
        const table = new DynamoTable(this, `${key}Table`, {
          tableName: `${prefix}-${key.toLowerCase()}`,
          ttlAttribute: ttl,
          globalSecondaryIndexes
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
