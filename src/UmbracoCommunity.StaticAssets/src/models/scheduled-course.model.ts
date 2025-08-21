type ScheduledCourseModel = {
    id: string;
    title: string;
    date: Date;
    price: number;
    currency: string;
    enrollUrl: string;
    region: string;
    localPrices: Object;
}