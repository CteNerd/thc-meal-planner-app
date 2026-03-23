import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import { Construct } from 'constructs';

export type GlobalSecondaryIndexConfig = {
  indexName: string;
  partitionKey: {
    name: string;
    type: dynamodb.AttributeType;
  };
  sortKey?: {
    name: string;
    type: dynamodb.AttributeType;
  };
  projectionType?: dynamodb.ProjectionType;
};

export type DynamoTableProps = {
  tableName: string;
  ttlAttribute?: string;
  globalSecondaryIndexes?: GlobalSecondaryIndexConfig[];
};

export class DynamoTable extends Construct {
  public readonly table: dynamodb.Table;

  public constructor(scope: Construct, id: string, props: DynamoTableProps) {
    super(scope, id);

    this.table = new dynamodb.Table(this, 'Resource', {
      tableName: props.tableName,
      partitionKey: { name: 'PK', type: dynamodb.AttributeType.STRING },
      sortKey: { name: 'SK', type: dynamodb.AttributeType.STRING },
      billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
      timeToLiveAttribute: props.ttlAttribute
    });

    props.globalSecondaryIndexes?.forEach((index) => {
      this.table.addGlobalSecondaryIndex({
        indexName: index.indexName,
        partitionKey: index.partitionKey,
        sortKey: index.sortKey,
        projectionType: index.projectionType ?? dynamodb.ProjectionType.ALL
      });
    });
  }
}
